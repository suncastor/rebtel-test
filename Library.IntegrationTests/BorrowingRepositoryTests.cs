using Library.Infrastructure.Entities;
using Library.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class BorrowingRepositoryTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddAsync_PersistsBorrowing_WithRelations()
    {
        int bookId, userId;
        await using (var seed = fixture.CreateContext())
        {
            var book = new Book { Title = "B", Author = "A", PageCount = 10, TotalCopies = 1 };
            var user = new User { FullName = "U", Email = "u@example.com" };
            seed.Books.Add(book);
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            bookId = book.Id;
            userId = user.Id;
        }

        await using (var db = fixture.CreateContext())
        {
            var repo = new BorrowingRepository(db);
            await repo.AddAsync(new Borrowing
            {
                BookId = bookId,
                UserId = userId,
                BorrowedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            await repo.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var borrowing = await verify.Borrowings
            .Include(b => b.Book)
            .Include(b => b.User)
            .SingleAsync();
        Assert.Equal("B", borrowing.Book.Title);
        Assert.Equal("u@example.com", borrowing.User.Email);
    }

    [Fact]
    public async Task AddAsync_WithMissingBook_Throws()
    {
        int userId;
        await using (var seed = fixture.CreateContext())
        {
            var user = new User { FullName = "U", Email = "u@example.com" };
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            userId = user.Id;
        }

        await using var db = fixture.CreateContext();
        var repo = new BorrowingRepository(db);
        await repo.AddAsync(new Borrowing
        {
            BookId = 99999,
            UserId = userId,
            BorrowedAt = DateTime.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.SaveChangesAsync());
    }

    [Fact]
    public async Task GetAll_GroupsByBookForMostBorrowedQuery()
    {
        await using (var seed = fixture.CreateContext())
        {
            var user = new User { FullName = "U", Email = "u@example.com" };
            var a = new Book { Title = "A", Author = "X", PageCount = 1, TotalCopies = 1 };
            var b = new Book { Title = "B", Author = "X", PageCount = 1, TotalCopies = 1 };
            seed.Users.Add(user);
            seed.Books.AddRange(a, b);
            await seed.SaveChangesAsync();

            var now = DateTime.UtcNow;
            seed.Borrowings.AddRange(
                new Borrowing { BookId = a.Id, UserId = user.Id, BorrowedAt = now },
                new Borrowing { BookId = a.Id, UserId = user.Id, BorrowedAt = now },
                new Borrowing { BookId = a.Id, UserId = user.Id, BorrowedAt = now },
                new Borrowing { BookId = b.Id, UserId = user.Id, BorrowedAt = now });
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var repo = new BorrowingRepository(db);

        var counts = await repo.GetAll()
            .Include(b => b.Book)
            .GroupBy(b => b.Book.Title)
            .Select(g => new { Title = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        Assert.Equal(2, counts.Count);
        Assert.Equal("A", counts[0].Title);
        Assert.Equal(3, counts[0].Count);
        Assert.Equal("B", counts[1].Title);
        Assert.Equal(1, counts[1].Count);
    }

    [Fact]
    public async Task DeletingBook_CascadesToBorrowings()
    {
        int bookId;
        await using (var seed = fixture.CreateContext())
        {
            var book = new Book { Title = "C", Author = "X", PageCount = 1, TotalCopies = 1 };
            var user = new User { FullName = "U", Email = "u@example.com" };
            seed.Books.Add(book);
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            bookId = book.Id;

            seed.Borrowings.Add(new Borrowing
            {
                BookId = book.Id,
                UserId = user.Id,
                BorrowedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
        {
            var book = await db.Books.FindAsync(bookId);
            db.Books.Remove(book!);
            await db.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        Assert.Empty(await verify.Borrowings.ToListAsync());
    }
}
