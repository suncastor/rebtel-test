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
    public async Task QueryCoBorrowedBooks_ReturnsOtherBooksBorrowedBySameUsers()
    {
        int targetBookId;
        await using (var seed = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var bob = new User { FullName = "Bob", Email = "bob@example.com" };
            var carol = new User { FullName = "Carol", Email = "carol@example.com" };
            seed.Users.AddRange(alice, bob, carol);

            var target = new Book { Title = "Target", Author = "A", PageCount = 100, TotalCopies = 1 };
            var b2 = new Book { Title = "Book 2", Author = "A", PageCount = 100, TotalCopies = 1 };
            var b3 = new Book { Title = "Book 3", Author = "A", PageCount = 100, TotalCopies = 1 };
            var unrelated = new Book { Title = "Unrelated", Author = "A", PageCount = 100, TotalCopies = 1 };
            seed.Books.AddRange(target, b2, b3, unrelated);
            await seed.SaveChangesAsync();
            targetBookId = target.Id;

            var now = DateTime.UtcNow;
            seed.Borrowings.AddRange(
                new Borrowing { UserId = alice.Id, BookId = target.Id, BorrowedAt = now },
                new Borrowing { UserId = bob.Id, BookId = target.Id, BorrowedAt = now },
                new Borrowing { UserId = alice.Id, BookId = b2.Id, BorrowedAt = now },
                new Borrowing { UserId = alice.Id, BookId = b2.Id, BorrowedAt = now },
                new Borrowing { UserId = bob.Id, BookId = b2.Id, BorrowedAt = now },
                new Borrowing { UserId = alice.Id, BookId = b3.Id, BorrowedAt = now },
                new Borrowing { UserId = carol.Id, BookId = unrelated.Id, BorrowedAt = now });
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var repo = new BorrowingRepository(db);

        var userIds = repo.GetAll()
            .Where(b => b.BookId == targetBookId)
            .Select(b => b.UserId)
            .Distinct();

        var coBorrowed = await repo.GetAll()
            .Where(b => b.BookId != targetBookId && userIds.Contains(b.UserId))
            .Include(b => b.Book)
            .GroupBy(b => new { b.BookId, b.Book.Title })
            .Select(g => new { g.Key.BookId, g.Key.Title, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        Assert.Equal(2, coBorrowed.Count);
        Assert.Equal("Book 2", coBorrowed[0].Title);
        Assert.Equal(3, coBorrowed[0].Count);
        Assert.Equal("Book 3", coBorrowed[1].Title);
        Assert.Equal(1, coBorrowed[1].Count);
        Assert.DoesNotContain(coBorrowed, c => c.Title == "Unrelated");
        Assert.DoesNotContain(coBorrowed, c => c.Title == "Target");
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
