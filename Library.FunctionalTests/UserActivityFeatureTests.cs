using Google.Protobuf.WellKnownTypes;
using Library.Application.Services;
using Library.Contracts.V1;
using Library.Infrastructure;
using Library.Infrastructure.Entities;
using Library.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Library.FunctionalTests;

[Collection(nameof(PostgresCollection))]
public class UserActivityFeatureTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private UsersService CreateService(AppDbContext db) => new(
        NullLogger<UsersService>.Instance,
        new BorrowingRepository(db));

    [Fact]
    public async Task GetTopBorrowers_RanksUsersByCountWithinTimeFrame()
    {
        var alice = await AddUserAsync("Alice", "alice@example.com");
        var bob = await AddUserAsync("Bob", "bob@example.com");
        var carol = await AddUserAsync("Carol", "carol@example.com");
        var book = await AddBookAsync("Book", pages: 100);

        var january = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var march = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var july = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        await AddBorrowingsAsync(
            (alice.Id, book.Id, january, null),
            (alice.Id, book.Id, march, null),
            (alice.Id, book.Id, july, null),
            (bob.Id, book.Id, march, null),
            (bob.Id, book.Id, march, null),
            (carol.Id, book.Id, july, null));

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var result = await service.GetTopBorrowers(new GetTopBorrowersRequest
        {
            From = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            To = Timestamp.FromDateTime(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            Top = 10
        }, null!);

        Assert.Equal(2, result.Borrowers.Count);
        Assert.All(result.Borrowers, b => Assert.Equal(2, b.BorrowCount));
        Assert.Contains(result.Borrowers, b => b.FullName == "Alice");
        Assert.Contains(result.Borrowers, b => b.FullName == "Bob");
        Assert.DoesNotContain(result.Borrowers, b => b.FullName == "Carol");
    }

    [Fact]
    public async Task GetTopBorrowers_WithoutTimeFrame_IncludesAllBorrowings()
    {
        var alice = await AddUserAsync("Alice", "alice@example.com");
        var bob = await AddUserAsync("Bob", "bob@example.com");
        var book = await AddBookAsync("Book", pages: 100);

        var now = DateTime.UtcNow;
        await AddBorrowingsAsync(
            (alice.Id, book.Id, now, null),
            (alice.Id, book.Id, now, null),
            (bob.Id, book.Id, now, null));

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var result = await service.GetTopBorrowers(new GetTopBorrowersRequest { Top = 10 }, null!);

        Assert.Equal(2, result.Borrowers.Count);
        Assert.Equal("Alice", result.Borrowers[0].FullName);
        Assert.Equal(2, result.Borrowers[0].BorrowCount);
    }

    [Fact]
    public async Task GetTopBorrowers_RespectsTopLimit()
    {
        for (int i = 1; i <= 5; i++)
        {
            var user = await AddUserAsync($"User {i}", $"user{i}@example.com");
            var book = await AddBookAsync($"Book {i}", pages: 100);
            await AddBorrowingsAsync(Enumerable.Range(0, i)
                .Select<int, (int, int, DateTime, DateTime?)>(_ => (user.Id, book.Id, DateTime.UtcNow, null))
                .ToArray());
        }

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var result = await service.GetTopBorrowers(new GetTopBorrowersRequest { Top = 3 }, null!);

        Assert.Equal(3, result.Borrowers.Count);
        Assert.Equal("User 5", result.Borrowers[0].FullName);
        Assert.Equal(5, result.Borrowers[0].BorrowCount);
        Assert.Equal("User 4", result.Borrowers[1].FullName);
        Assert.Equal("User 3", result.Borrowers[2].FullName);
    }

    [Fact]
    public async Task EstimateReadingPace_AveragesPagesPerDayAcrossCompletedBorrowings()
    {
        var alice = await AddUserAsync("Alice", "alice@example.com");
        var book300 = await AddBookAsync("A", pages: 300);
        var book200 = await AddBookAsync("B", pages: 200);

        var start = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        await AddBorrowingsAsync(
            (alice.Id, book300.Id, start, start.AddDays(10)),
            (alice.Id, book200.Id, start, start.AddDays(20)));

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var result = await service.EstimateReadingPace(
            new EstimateReadingPaceRequest { UserId = alice.Id },
            null!);

        Assert.True(result.HasPagesPerDay);
        Assert.Equal(20.0, result.PagesPerDay, 3);
    }

    [Fact]
    public async Task EstimateReadingPace_IgnoresUnreturnedBorrowings()
    {
        var alice = await AddUserAsync("Alice", "alice@example.com");
        var book = await AddBookAsync("B", pages: 100);

        var start = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        await AddBorrowingsAsync(
            (alice.Id, book.Id, start, start.AddDays(10)),
            (alice.Id, book.Id, start, null));

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var result = await service.EstimateReadingPace(
            new EstimateReadingPaceRequest { UserId = alice.Id },
            null!);

        Assert.Equal(10.0, result.PagesPerDay, 3);
    }

    [Fact]
    public async Task EstimateReadingPace_UserHasNoCompletedBorrowings_ReturnsUnset()
    {
        var alice = await AddUserAsync("Alice", "alice@example.com");

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var result = await service.EstimateReadingPace(
            new EstimateReadingPaceRequest { UserId = alice.Id },
            null!);

        Assert.False(result.HasPagesPerDay);
    }

    private async Task<User> AddUserAsync(string name, string email)
    {
        await using var db = fixture.CreateContext();
        var user = new User { FullName = name, Email = email };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<Book> AddBookAsync(string title, int pages)
    {
        await using var db = fixture.CreateContext();
        var book = new Book { Title = title, Author = "X", PageCount = pages, TotalCopies = 1 };
        db.Books.Add(book);
        await db.SaveChangesAsync();
        return book;
    }

    private async Task AddBorrowingsAsync(params (int UserId, int BookId, DateTime BorrowedAt, DateTime? ReturnedAt)[] rows)
    {
        await using var db = fixture.CreateContext();
        foreach (var r in rows)
        {
            db.Borrowings.Add(new Borrowing
            {
                UserId = r.UserId,
                BookId = r.BookId,
                BorrowedAt = r.BorrowedAt,
                ReturnedAt = r.ReturnedAt
            });
        }

        await db.SaveChangesAsync();
    }
}
