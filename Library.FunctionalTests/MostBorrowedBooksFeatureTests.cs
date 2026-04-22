using Library.Application.Services;
using Library.Contracts.V1;
using Library.Infrastructure;
using Library.Infrastructure.Entities;
using Library.Infrastructure.Repositories;

namespace Library.FunctionalTests;

[Collection(nameof(PostgresCollection))]
public class MostBorrowedBooksFeatureTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private BooksService CreateService(AppDbContext db)
    {
        var repo = new BorrowingRepository(db);
        return new BooksService(repo);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_RanksByBorrowCountDescending()
    {
        await SeedBorrowingsAsync(
            ("Moby Dick", 5),
            ("1984", 3),
            ("Dune", 8),
            ("Hyperion", 1));

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetMostBorrowedBooks(
            new GetMostBorrowedBooksRequest { Top = 10 },
            context: null!);

        Assert.Equal(4, response.Books.Count);
        Assert.Equal("Dune", response.Books[0].Title);
        Assert.Equal(8, response.Books[0].BorrowCount);
        Assert.Equal("Moby Dick", response.Books[1].Title);
        Assert.Equal("1984", response.Books[2].Title);
        Assert.Equal("Hyperion", response.Books[3].Title);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_RespectsTopLimit()
    {
        await SeedBorrowingsAsync(
            ("A", 5),
            ("B", 4),
            ("C", 3),
            ("D", 2),
            ("E", 1));

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetMostBorrowedBooks(
            new GetMostBorrowedBooksRequest { Top = 3 },
            context: null!);

        Assert.Equal(3, response.Books.Count);
        Assert.Equal(new[] { "A", "B", "C" }, response.Books.Select(b => b.Title));
    }

    [Fact]
    public async Task GetMostBorrowedBooks_ClampsZeroToDefaultTen()
    {
        var rows = Enumerable.Range(1, 15).Select(i => ($"Book {i:D2}", i)).ToArray();
        await SeedBorrowingsAsync(rows);

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetMostBorrowedBooks(
            new GetMostBorrowedBooksRequest { Top = 0 },
            context: null!);

        Assert.Equal(10, response.Books.Count);
        Assert.Equal("Book 15", response.Books[0].Title);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_ClampsOversizedTopToHundred()
    {
        var rows = Enumerable.Range(1, 120).Select(i => ($"Title {i:D3}", 1)).ToArray();
        await SeedBorrowingsAsync(rows);

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetMostBorrowedBooks(
            new GetMostBorrowedBooksRequest { Top = 500 },
            context: null!);

        Assert.Equal(100, response.Books.Count);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_ReturnsEmpty_WhenNoBorrowings()
    {
        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetMostBorrowedBooks(
            new GetMostBorrowedBooksRequest { Top = 10 },
            context: null!);

        Assert.Empty(response.Books);
    }

    [Fact]
    public async Task GetMostBorrowedBooks_CountsOnlyActualBorrowings_IgnoresUnborrowedBooks()
    {
        await using (var seed = fixture.CreateContext())
        {
            seed.Books.Add(new Book { Title = "Never Borrowed", Author = "X", PageCount = 1, TotalCopies = 1 });
            await seed.SaveChangesAsync();
        }

        await SeedBorrowingsAsync(("Borrowed Once", 1));

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetMostBorrowedBooks(
            new GetMostBorrowedBooksRequest { Top = 10 },
            context: null!);

        Assert.Single(response.Books);
        Assert.Equal("Borrowed Once", response.Books[0].Title);
    }

    private async Task SeedBorrowingsAsync(params (string Title, int BorrowCount)[] rows)
    {
        await using var db = fixture.CreateContext();

        var user = new User { FullName = "Reader", Email = "reader@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        foreach (var (title, count) in rows)
        {
            var book = new Book { Title = title, Author = "Author", PageCount = 100, TotalCopies = 1 };
            db.Books.Add(book);
            await db.SaveChangesAsync();

            var now = DateTime.UtcNow;
            for (int i = 0; i < count; i++)
            {
                db.Borrowings.Add(new Borrowing
                {
                    BookId = book.Id,
                    UserId = user.Id,
                    BorrowedAt = now.AddMinutes(-i),
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
