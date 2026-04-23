using Google.Protobuf.WellKnownTypes;
using Library.Application.Services;
using Library.Contracts.V1;
using Library.Infrastructure.Entities;
using Library.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using Moq;

namespace Library.UnitTests;

public class UsersServiceTests
{
    private readonly Mock<IBorrowingRepository> _mockRepo = new();
    private readonly UsersService _service;

    public UsersServiceTests()
    {
        _service = new UsersService(NullLogger<UsersService>.Instance, _mockRepo.Object);
    }

    private static Borrowing MakeBorrowing(
        int userId,
        string userName,
        int bookId = 1,
        int pageCount = 100,
        DateTime? borrowedAt = null,
        DateTime? returnedAt = null) =>
        new()
        {
            UserId = userId,
            BookId = bookId,
            BorrowedAt = borrowedAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ReturnedAt = returnedAt,
            User = new User { Id = userId, FullName = userName, Email = $"{userName}@example.com" },
            Book = new Book { Id = bookId, Title = $"Book {bookId}", PageCount = pageCount }
        };

    [Fact]
    public async Task GetTopBorrowers_RanksByCountDescending()
    {
        var data = new List<Borrowing>
        {
            MakeBorrowing(1, "Alice"),
            MakeBorrowing(1, "Alice"),
            MakeBorrowing(1, "Alice"),
            MakeBorrowing(2, "Bob"),
            MakeBorrowing(2, "Bob"),
            MakeBorrowing(3, "Carol"),
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.GetTopBorrowers(
            new GetTopBorrowersRequest { Top = 10 },
            null!);

        Assert.Equal(3, result.Borrowers.Count);
        Assert.Equal("Alice", result.Borrowers[0].FullName);
        Assert.Equal(3, result.Borrowers[0].BorrowCount);
        Assert.Equal("Bob", result.Borrowers[1].FullName);
        Assert.Equal("Carol", result.Borrowers[2].FullName);
    }

    [Fact]
    public async Task GetTopBorrowers_FiltersByTimeFrame()
    {
        var january = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var march = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var june = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var data = new List<Borrowing>
        {
            MakeBorrowing(1, "Alice", borrowedAt: january),
            MakeBorrowing(1, "Alice", borrowedAt: march),
            MakeBorrowing(2, "Bob", borrowedAt: june),
            MakeBorrowing(2, "Bob", borrowedAt: june),
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var request = new GetTopBorrowersRequest
        {
            From = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            To = Timestamp.FromDateTime(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            Top = 10
        };

        var result = await _service.GetTopBorrowers(request, null!);

        Assert.Single(result.Borrowers);
        Assert.Equal("Alice", result.Borrowers[0].FullName);
        Assert.Equal(2, result.Borrowers[0].BorrowCount);
    }

    [Fact]
    public async Task GetTopBorrowers_WhenInputTopIsZero_ClampsTopValue()
    {
        var data = Enumerable.Range(1, 20)
            .Select(i => MakeBorrowing(i, $"User {i}"))
            .ToList();
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.GetTopBorrowers(
            new GetTopBorrowersRequest { Top = 0 },
            null!);

        Assert.Equal(10, result.Borrowers.Count);
    }

    [Fact]
    public async Task GetTopBorrowers_ClampsTopValueToOneHundred()
    {
        var data = Enumerable.Range(1, 200)
            .Select(i => MakeBorrowing(i, $"User {i}"))
            .ToList();
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.GetTopBorrowers(
            new GetTopBorrowersRequest { Top = 500 },
            null!);

        Assert.Equal(100, result.Borrowers.Count);
    }

    [Fact]
    public async Task GetTopBorrowers_NoData_ReturnsEmpty()
    {
        _mockRepo.Setup(r => r.GetAll()).Returns(new List<Borrowing>().AsQueryable().BuildMock());

        var result = await _service.GetTopBorrowers(
            new GetTopBorrowersRequest { Top = 10 },
            null!);

        Assert.Empty(result.Borrowers);
    }

    [Fact]
    public async Task EstimateReadingPace_AveragesPagesPerDayOnReturnedBorrowings()
    {
        var borrowed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var data = new List<Borrowing>
        {
            MakeBorrowing(
                1,
                "Alice",
                bookId: 1,
                pageCount: 300,
                borrowedAt: borrowed,
                returnedAt: borrowed.AddDays(10)), // 30 p/d
            MakeBorrowing(
                1,
                "Alice",
                bookId: 2,
                pageCount: 200,
                borrowedAt: borrowed,
                returnedAt: borrowed.AddDays(20)), // 10 p/d
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.EstimateReadingPace(
            new EstimateReadingPaceRequest { UserId = 1 },
            null!);

        Assert.True(result.HasPagesPerDay);
        Assert.Equal(20.0, result.PagesPerDay, 3);
    }

    [Fact]
    public async Task EstimateReadingPace_IgnoresUnreturnedBorrowings()
    {
        var borrowed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var data = new List<Borrowing>
        {
            MakeBorrowing(
                1,
                "Alice",
                pageCount: 100,
                borrowedAt: borrowed,
                returnedAt: borrowed.AddDays(10)), // 10 p/d
            MakeBorrowing(
                1,
                "Alice",
                pageCount: 500,
                borrowedAt: borrowed,
                returnedAt: null), // ignored
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.EstimateReadingPace(
            new EstimateReadingPaceRequest { UserId = 1 },
            null!);

        Assert.Equal(10.0, result.PagesPerDay, 3);
    }

    [Fact]
    public async Task EstimateReadingPace_SkipsSameDayReturns()
    {
        var borrowed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var data = new List<Borrowing>
        {
            MakeBorrowing(
                1,
                "Alice",
                pageCount: 100,
                borrowedAt: borrowed,
                returnedAt: borrowed), // 0 days, skipped
            MakeBorrowing(
                1,
                "Alice",
                pageCount: 200,
                borrowedAt: borrowed,
                returnedAt: borrowed.AddDays(5)), // 40 p/d
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.EstimateReadingPace(
            new EstimateReadingPaceRequest { UserId = 1 },
            null!);

        Assert.Equal(40.0, result.PagesPerDay, 3);
    }

    [Fact]
    public async Task EstimateReadingPace_NoCompletedBorrowings_ReturnsUnset()
    {
        _mockRepo.Setup(r => r.GetAll()).Returns(new List<Borrowing>().AsQueryable().BuildMock());

        var result = await _service.EstimateReadingPace(
            new EstimateReadingPaceRequest { UserId = 42 },
            null!);

        Assert.False(result.HasPagesPerDay);
        Assert.Equal(42, result.UserId);
    }

    [Fact]
    public async Task EstimateReadingPace_IsolatesByUser()
    {
        var borrowed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var data = new List<Borrowing>
        {
            MakeBorrowing(1, "Alice", pageCount: 100, borrowedAt: borrowed, returnedAt: borrowed.AddDays(10)),
            MakeBorrowing(2, "Bob", pageCount: 500, borrowedAt: borrowed, returnedAt: borrowed.AddDays(5)),
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.EstimateReadingPace(
            new EstimateReadingPaceRequest { UserId = 1 },
            null!);

        Assert.Equal(10.0, result.PagesPerDay, 3);
    }
}
