using Moq;
using MockQueryable.Moq;
using Grpc.Core;
using Library.Infrastructure.Repositories;
using Library.Application.Services;
using Library.Infrastructure.Entities;
using Library.Contracts.V1;
using Microsoft.Extensions.Logging.Abstractions;

namespace Library.UnitTests;

public class BooksServiceTests
{
    private readonly Mock<IBorrowingRepository> _mockRepo;
    private readonly BooksService _service;

    public BooksServiceTests()
    {
        _mockRepo = new Mock<IBorrowingRepository>();
        _service = new BooksService(NullLogger<BooksService>.Instance, _mockRepo.Object);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_ReturnsCorrectGroupsAndCounts()
    {
        var data = new List<Borrowing>
        {
            new() { BookId = 1, Book = new Book { Id = 1, Title = "Book A" } },
            new() { BookId = 1, Book = new Book { Id = 1, Title = "Book A" } },
            new() { BookId = 1, Book = new Book { Id = 1, Title = "Book A" } },
            new() { BookId = 1, Book = new Book { Id = 1, Title = "Book A" } },
            new() { BookId = 2, Book = new Book { Id = 2, Title = "Book B" } },
            new() { BookId = 2, Book = new Book { Id = 2, Title = "Book B" } },
            new() { BookId = 2, Book = new Book { Id = 2, Title = "Book B" } },
            new() { BookId = 3, Book = new Book { Id = 3, Title = "Book C" } },
            new() { BookId = 3, Book = new Book { Id = 4, Title = "Book C" } },
        };

        var mockDbSet = data.AsQueryable().BuildMock();
        _mockRepo.Setup(r => r.GetAll()).Returns(mockDbSet);

        var request = new GetMostBorrowedBooksRequest { Top = 0 };

        ServerCallContext context = null!;

        var result = await _service.GetMostBorrowedBooks(request, context);

        Assert.NotNull(result);
        Assert.Equal(3, result.Books.Count);
        Assert.Equal("Book A", result.Books[0].Title);
        Assert.Equal(4, result.Books[0].BorrowCount);
        Assert.Equal("Book B", result.Books[1].Title);
        Assert.Equal(3, result.Books[1].BorrowCount);
        Assert.Equal("Book C", result.Books[2].Title);
        Assert.Equal(2, result.Books[2].BorrowCount);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_InputTopIsZero_ClampsTopValue()
    {
        var mockDbSet = Enumerable.Range(0, 20).Select(i => new Borrowing
        {
            BookId = i,
            Book = new Book { Id = i, Title = $"Book {i}" },
        }).AsQueryable().BuildMock();
        _mockRepo.Setup(r => r.GetAll()).Returns(mockDbSet);

        var request = new GetMostBorrowedBooksRequest { Top = 0 };

        ServerCallContext context = null!;

        var result = await _service.GetMostBorrowedBooks(request, context);

        Assert.NotNull(result);
        Assert.Equal(10, result.Books.Count);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_LimitsToMaxOneHundred()
    {
        var mockDbSet = Enumerable.Range(0, 200).Select(i => new Borrowing
        {
            BookId = i,
            Book = new Book { Id = i, Title = $"Book {i}" },
        }).AsQueryable().BuildMock();
        _mockRepo.Setup(r => r.GetAll()).Returns(mockDbSet);

        var request = new GetMostBorrowedBooksRequest { Top = 500 };

        var result = await _service.GetMostBorrowedBooks(request, null!);

        Assert.Equal(100, result.Books.Count);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_NoDataExists_ReturnsEmpty()
    {
        // Arrange
        var emptyData = new List<Borrowing>().AsQueryable().BuildMock();
        _mockRepo.Setup(r => r.GetAll()).Returns(emptyData);

        var request = new GetMostBorrowedBooksRequest { Top = 10 };

        // Act
        var result = await _service.GetMostBorrowedBooks(request, null!);

        // Assert
        Assert.Empty(result.Books);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_HandlesMultipleBooksWithSameBorrowCount()
    {
        // Arrange
        var tieData = new List<Borrowing>
        {
            new() { BookId = 1, Book = new Book { Id = 1, Title = "Book A" } },
            new() { BookId = 2, Book = new Book { Id = 2, Title = "Book B" } }
        }.AsQueryable().BuildMock();

        _mockRepo.Setup(r => r.GetAll()).Returns(tieData);

        var request = new GetMostBorrowedBooksRequest { Top = 10 };

        // Act
        var result = await _service.GetMostBorrowedBooks(request, null!);

        // Assert
        Assert.Equal(2, result.Books.Count);
        Assert.All(result.Books, b => Assert.Equal(1, b.BorrowCount));
    }

    [Fact]
    public async Task GetCoBorrowedBooks_ReturnsBooksBorrowedByTheSameUsersExcludingTargetBook()
    {
        // Users 1 and 2 borrowed Book 1.
        // Also: User 1 borrowed Book 2 (twice) and Book 3. User 2 borrowed Book 2.
        // User 3 never borrowed Book 1, so their borrowings of Book 4 must not appear.
        var data = new List<Borrowing>
        {
            new() { BookId = 1, UserId = 1, Book = new Book { Id = 1, Title = "Target" } },
            new() { BookId = 1, UserId = 2, Book = new Book { Id = 1, Title = "Target" } },
            new() { BookId = 2, UserId = 1, Book = new Book { Id = 2, Title = "Book B" } },
            new() { BookId = 2, UserId = 1, Book = new Book { Id = 2, Title = "Book B" } },
            new() { BookId = 2, UserId = 2, Book = new Book { Id = 2, Title = "Book B" } },
            new() { BookId = 3, UserId = 1, Book = new Book { Id = 3, Title = "Book C" } },
            new() { BookId = 4, UserId = 3, Book = new Book { Id = 4, Title = "Unrelated" } },
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.GetCoBorrowedBooks(
            new GetCoBorrowedBooksRequest { BookId = 1, Top = 10 },
            null!);

        Assert.Equal(2, result.Books.Count);
        Assert.Equal("Book B", result.Books[0].Title);
        Assert.Equal(3, result.Books[0].BorrowCount);
        Assert.Equal("Book C", result.Books[1].Title);
        Assert.Equal(1, result.Books[1].BorrowCount);
        Assert.DoesNotContain(result.Books, b => b.Title == "Target");
        Assert.DoesNotContain(result.Books, b => b.Title == "Unrelated");
    }

    [Fact]
    public async Task GetCoBorrowedBooks_TargetBookHasNoBorrowers_ReturnsEmpty()
    {
        var data = new List<Borrowing>
        {
            new() { BookId = 2, UserId = 1, Book = new Book { Id = 2, Title = "Book B" } },
            new() { BookId = 3, UserId = 2, Book = new Book { Id = 3, Title = "Book C" } },
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.GetCoBorrowedBooks(
            new GetCoBorrowedBooksRequest { BookId = 999, Top = 10 },
            null!);

        Assert.Empty(result.Books);
    }

    [Fact]
    public async Task GetCoBorrowedBooks_TopIsZero_ClampsTopValue()
    {
        var borrowings = new List<Borrowing>
        {
            new() { BookId = 1, UserId = 1, Book = new Book { Id = 1, Title = "Target" } }
        };
        borrowings.AddRange(Enumerable.Range(2, 20).Select(i => new Borrowing
        {
            BookId = i,
            UserId = 1,
            Book = new Book { Id = i, Title = $"Book {i}" }
        }));
        _mockRepo.Setup(r => r.GetAll()).Returns(borrowings.AsQueryable().BuildMock());

        var result = await _service.GetCoBorrowedBooks(
            new GetCoBorrowedBooksRequest { BookId = 1, Top = 0 },
            null!);

        Assert.Equal(10, result.Books.Count);
    }

    [Fact]
    public async Task GetCoBorrowedBooks_TopIsMoreThan100_ClampsTopValue()
    {
        var borrowings = new List<Borrowing>
        {
            new() { BookId = 1, UserId = 1, Book = new Book { Id = 1, Title = "Target" } }
        };
        borrowings.AddRange(Enumerable.Range(2, 200).Select(i => new Borrowing
        {
            BookId = i,
            UserId = 1,
            Book = new Book { Id = i, Title = $"Book {i}" }
        }));
        _mockRepo.Setup(r => r.GetAll()).Returns(borrowings.AsQueryable().BuildMock());

        var result = await _service.GetCoBorrowedBooks(
            new GetCoBorrowedBooksRequest { BookId = 1, Top = 500 },
            null!);

        Assert.Equal(100, result.Books.Count);
    }

    [Fact]
    public async Task GetCoBorrowedBooks_RequestBookIdIsNotPositive_ThrowsInvalidArgument()
    {
        _mockRepo.Setup(r => r.GetAll()).Returns(new List<Borrowing>().AsQueryable().BuildMock());

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            _service.GetCoBorrowedBooks(new GetCoBorrowedBooksRequest { BookId = 0, Top = 10 },
            null!));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task GetCoBorrowedBooks_SameUserBorrowsTargetMultipleTimes_DoesNotDoubleCount()
    {
        // User 1 borrowed the target book twice, and Book 2 once.
        // Co-borrow count for Book 2 must be 1 (one borrowing), not multiplied by target-borrow count.
        var data = new List<Borrowing>
        {
            new() { BookId = 1, UserId = 1, Book = new Book { Id = 1, Title = "Target" } },
            new() { BookId = 1, UserId = 1, Book = new Book { Id = 1, Title = "Target" } },
            new() { BookId = 2, UserId = 1, Book = new Book { Id = 2, Title = "Book B" } },
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(data.AsQueryable().BuildMock());

        var result = await _service.GetCoBorrowedBooks(
            new GetCoBorrowedBooksRequest { BookId = 1, Top = 10 },
            null!);

        Assert.Single(result.Books);
        Assert.Equal("Book B", result.Books[0].Title);
        Assert.Equal(1, result.Books[0].BorrowCount);
    }
}