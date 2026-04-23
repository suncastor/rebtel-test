using Library.Infrastructure.Entities;
using Library.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class UserActivityQueryTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Query_BorrowedAtDateRangeFilter_ReturnsFilteredByDate()
    {
        await using (var seed = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var bob = new User { FullName = "Bob", Email = "bob@example.com" };
            var book = new Book { Title = "B", Author = "X", PageCount = 1, TotalCopies = 1 };
            seed.Users.AddRange(alice, bob);
            seed.Books.Add(book);
            await seed.SaveChangesAsync();

            var dates = new[]
            {
                new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            };
            seed.Borrowings.AddRange(
                new Borrowing { UserId = alice.Id, BookId = book.Id, BorrowedAt = dates[0] },
                new Borrowing { UserId = alice.Id, BookId = book.Id, BorrowedAt = dates[1] },
                new Borrowing { UserId = bob.Id, BookId = book.Id, BorrowedAt = dates[2] },
                new Borrowing { UserId = bob.Id, BookId = book.Id, BorrowedAt = dates[3] });
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var repo = new BorrowingRepository(db);

        var from = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);

        var counts = await repo.GetAll()
            .Where(b => b.BorrowedAt >= from && b.BorrowedAt <= to)
            .Include(b => b.User)
            .GroupBy(b => new { b.UserId, b.User.FullName })
            .Select(g => new { g.Key.FullName, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        Assert.Equal(2, counts.Count);
        Assert.Contains(counts, c => c.FullName == "Alice" && c.Count == 1);
        Assert.Contains(counts, c => c.FullName == "Bob" && c.Count == 1);
    }

    [Fact]
    public async Task Query_ReturnedAtNullCheckFilter_ExcludesUnreturnedBorrowings()
    {
        int userId;
        await using (var seed = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var book = new Book { Title = "B", Author = "X", PageCount = 100, TotalCopies = 1 };
            seed.Users.Add(alice);
            seed.Books.Add(book);
            await seed.SaveChangesAsync();
            userId = alice.Id;

            var start = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            seed.Borrowings.AddRange(
                new Borrowing { UserId = alice.Id, BookId = book.Id, BorrowedAt = start, ReturnedAt = start.AddDays(5) },
                new Borrowing { UserId = alice.Id, BookId = book.Id, BorrowedAt = start, ReturnedAt = null });
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var repo = new BorrowingRepository(db);

        var completed = await repo.GetAll()
            .Where(b => b.UserId == userId && b.ReturnedAt != null)
            .CountAsync();

        Assert.Equal(1, completed);
    }

    [Fact]
    public async Task Query_IncludeBooks_JoinsBookPageCount()
    {
        int userId;
        await using (var seed = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var book = new Book { Title = "Long Book", Author = "X", PageCount = 500, TotalCopies = 1 };
            seed.Users.Add(alice);
            seed.Books.Add(book);
            await seed.SaveChangesAsync();
            userId = alice.Id;

            var start = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            seed.Borrowings.Add(new Borrowing
            {
                UserId = alice.Id,
                BookId = book.Id,
                BorrowedAt = start,
                ReturnedAt = start.AddDays(10)
            });
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var repo = new BorrowingRepository(db);

        var projected = await repo.GetAll()
            .Where(b => b.UserId == userId && b.ReturnedAt != null)
            .Include(b => b.Book)
            .Select(b => new { b.Book.PageCount, b.BorrowedAt, ReturnedAt = b.ReturnedAt!.Value })
            .ToListAsync();

        Assert.Single(projected);
        Assert.Equal(500, projected[0].PageCount);
        Assert.Equal(10, (int)(projected[0].ReturnedAt - projected[0].BorrowedAt).TotalDays);
    }
}
