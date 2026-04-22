using Moq;
using MockQueryable.Moq;
using Grpc.Core;
using Library.Infrastructure.Repositories;
using Library.Application.Services;
using Library.Infrastructure.Entities;
using Library.Contracts.V1;

namespace Library.UnitTests;

public class BooksServiceTests
{
    private readonly Mock<IBorrowingRepository> _mockRepo;
    private readonly BooksService _service;

    public BooksServiceTests()
    {
        _mockRepo = new Mock<IBorrowingRepository>();
        _service = new BooksService(_mockRepo.Object);
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
    public async Task GetMostBorrowedBooks_ClampsTopValue_WhenInputIsZero()
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
    public async Task GetMostBorrowedBooks_ReturnsEmpty_WhenNoDataExists()
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
}