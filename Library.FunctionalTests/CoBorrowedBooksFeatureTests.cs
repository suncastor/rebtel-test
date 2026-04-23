using Grpc.Core;
using Library.Application.Services;
using Library.Contracts.V1;
using Library.Infrastructure;
using Library.Infrastructure.Entities;
using Library.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Library.FunctionalTests;

[Collection(nameof(PostgresCollection))]
public class CoBorrowedBooksFeatureTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private BooksService CreateService(AppDbContext db)
    {
        var repo = new BorrowingRepository(db);
        return new BooksService(NullLogger<BooksService>.Instance, repo);
    }

    [Fact]
    public async Task GetCoBorrowedBooks_ReturnsOtherBooksBorrowedBySameUsersRankedByCount()
    {
        int targetId;
        await using (var seed = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var bob = new User { FullName = "Bob", Email = "bob@example.com" };
            var carol = new User { FullName = "Carol", Email = "carol@example.com" };
            seed.Users.AddRange(alice, bob, carol);

            var target = new Book { Title = "Target", Author = "A", PageCount = 100, TotalCopies = 1 };
            var popular = new Book { Title = "Popular", Author = "A", PageCount = 100, TotalCopies = 1 };
            var niche = new Book { Title = "Niche", Author = "A", PageCount = 100, TotalCopies = 1 };
            var unrelated = new Book { Title = "Unrelated", Author = "A", PageCount = 100, TotalCopies = 1 };
            seed.Books.AddRange(target, popular, niche, unrelated);
            await seed.SaveChangesAsync();
            targetId = target.Id;

            var now = DateTime.UtcNow;
            seed.Borrowings.AddRange(
                new Borrowing { UserId = alice.Id, BookId = target.Id, BorrowedAt = now },
                new Borrowing { UserId = bob.Id, BookId = target.Id, BorrowedAt = now },
                new Borrowing { UserId = alice.Id, BookId = popular.Id, BorrowedAt = now },
                new Borrowing { UserId = alice.Id, BookId = popular.Id, BorrowedAt = now },
                new Borrowing { UserId = bob.Id, BookId = popular.Id, BorrowedAt = now },
                new Borrowing { UserId = bob.Id, BookId = niche.Id, BorrowedAt = now },
                new Borrowing { UserId = carol.Id, BookId = unrelated.Id, BorrowedAt = now });
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetCoBorrowedBooks(
            new GetCoBorrowedBooksRequest { BookId = targetId, Top = 10 },
            context: null!);

        Assert.Equal(2, response.Books.Count);
        Assert.Equal("Popular", response.Books[0].Title);
        Assert.Equal(3, response.Books[0].BorrowCount);
        Assert.Equal("Niche", response.Books[1].Title);
        Assert.Equal(1, response.Books[1].BorrowCount);
        Assert.DoesNotContain(response.Books, b => b.Title == "Unrelated");
        Assert.DoesNotContain(response.Books, b => b.Title == "Target");
    }

    [Fact]
    public async Task GetCoBorrowedBooks_TargetBookWasNeverBorrowed_ReturnsEmpty()
    {
        int targetId;
        await using (var seed = fixture.CreateContext())
        {
            var user = new User { FullName = "U", Email = "u@example.com" };
            var target = new Book { Title = "Target", Author = "A", PageCount = 100, TotalCopies = 1 };
            var other = new Book { Title = "Other", Author = "A", PageCount = 100, TotalCopies = 1 };
            seed.Users.Add(user);
            seed.Books.AddRange(target, other);
            await seed.SaveChangesAsync();
            targetId = target.Id;

            seed.Borrowings.Add(new Borrowing
            {
                UserId = user.Id,
                BookId = other.Id,
                BorrowedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetCoBorrowedBooks(
            new GetCoBorrowedBooksRequest { BookId = targetId, Top = 10 },
            context: null!);

        Assert.Empty(response.Books);
    }

    [Fact]
    public async Task GetCoBorrowedBooks_RespectsTopLimit()
    {
        int targetId;
        await using (var seed = fixture.CreateContext())
        {
            var user = new User { FullName = "U", Email = "u@example.com" };
            seed.Users.Add(user);
            var target = new Book { Title = "Target", Author = "A", PageCount = 100, TotalCopies = 1 };
            seed.Books.Add(target);
            await seed.SaveChangesAsync();
            targetId = target.Id;

            seed.Borrowings.Add(new Borrowing
            {
                UserId = user.Id,
                BookId = target.Id,
                BorrowedAt = DateTime.UtcNow
            });

            for (int i = 1; i <= 5; i++)
            {
                var book = new Book { Title = $"Book {i}", Author = "A", PageCount = 100, TotalCopies = 1 };
                seed.Books.Add(book);
                await seed.SaveChangesAsync();
                for (int j = 0; j < i; j++)
                {
                    seed.Borrowings.Add(new Borrowing
                    {
                        UserId = user.Id,
                        BookId = book.Id,
                        BorrowedAt = DateTime.UtcNow.AddMinutes(-j)
                    });
                }
            }
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetCoBorrowedBooks(
            new GetCoBorrowedBooksRequest { BookId = targetId, Top = 3 },
            context: null!);

        Assert.Equal(3, response.Books.Count);
        Assert.Equal(new[] { "Book 5", "Book 4", "Book 3" }, response.Books.Select(b => b.Title));
    }

    [Fact]
    public async Task GetCoBorrowedBooks_ClampsZeroTopToDefaultTen()
    {
        int targetId;
        await using (var seed = fixture.CreateContext())
        {
            var user = new User { FullName = "U", Email = "u@example.com" };
            seed.Users.Add(user);
            var target = new Book { Title = "Target", Author = "A", PageCount = 100, TotalCopies = 1 };
            seed.Books.Add(target);
            await seed.SaveChangesAsync();
            targetId = target.Id;

            seed.Borrowings.Add(new Borrowing
            {
                UserId = user.Id,
                BookId = target.Id,
                BorrowedAt = DateTime.UtcNow
            });

            for (int i = 1; i <= 15; i++)
            {
                var book = new Book { Title = $"Book {i:D2}", Author = "A", PageCount = 100, TotalCopies = 1 };
                seed.Books.Add(book);
                await seed.SaveChangesAsync();
                seed.Borrowings.Add(new Borrowing
                {
                    UserId = user.Id,
                    BookId = book.Id,
                    BorrowedAt = DateTime.UtcNow
                });
            }
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var response = await service.GetCoBorrowedBooks(
            new GetCoBorrowedBooksRequest { BookId = targetId, Top = 0 },
            context: null!);

        Assert.Equal(10, response.Books.Count);
    }

    [Fact]
    public async Task GetCoBorrowedBooks_RequestBookIdNotPositive_ThrowsInvalidArgument()
    {
        await using var db = fixture.CreateContext();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetCoBorrowedBooks(
                new GetCoBorrowedBooksRequest { BookId = 0, Top = 10 },
                context: null!));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }
}
