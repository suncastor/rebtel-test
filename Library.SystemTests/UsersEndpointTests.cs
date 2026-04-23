using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Library.Infrastructure.Entities;

namespace Library.SystemTests;

[Collection(nameof(LibrarySystemCollection))]
public class UsersEndpointTests(LibrarySystemFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private record TopBorrowerDto(int UserId, string FullName, int BorrowCount);

    private record ReadingPaceDto(int UserId, double? PagesPerDay);

    [Fact]
    public async Task GetTopBorrowers_ReturnsRankedUsers_FromHttpThroughGrpcToDb()
    {
        int aliceId, bobId, bookId;
        await using (var db = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var bob = new User { FullName = "Bob", Email = "bob@example.com" };
            var book = new Book { Title = "B", Author = "X", PageCount = 100, TotalCopies = 1 };
            db.Users.AddRange(alice, bob);
            db.Books.Add(book);
            await db.SaveChangesAsync();

            aliceId = alice.Id;
            bobId = bob.Id;
            bookId = book.Id;

            var now = DateTime.UtcNow;
            db.Borrowings.AddRange(
                new Borrowing { UserId = aliceId, BookId = bookId, BorrowedAt = now },
                new Borrowing { UserId = aliceId, BookId = bookId, BorrowedAt = now },
                new Borrowing { UserId = aliceId, BookId = bookId, BorrowedAt = now },
                new Borrowing { UserId = bobId, BookId = bookId, BorrowedAt = now });
            await db.SaveChangesAsync();
        }

        var response = await fixture.ApiClient.GetAsync("/Users/top-borrowers?top=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var borrowers = JsonSerializer.Deserialize<List<TopBorrowerDto>>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(2, borrowers.Count);
        Assert.Equal("Alice", borrowers[0].FullName);
        Assert.Equal(3, borrowers[0].BorrowCount);
        Assert.Equal("Bob", borrowers[1].FullName);
        Assert.Equal(1, borrowers[1].BorrowCount);
    }

    [Fact]
    public async Task GetTopBorrowers_ReturnsRankedUsers_FilteredByBorrowedAtDate()
    {
        var now = DateTime.UtcNow;
        int aliceId, bobId, bookId;
        await using (var db = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var bob = new User { FullName = "Bob", Email = "bob@example.com" };
            var book = new Book { Title = "B", Author = "X", PageCount = 100, TotalCopies = 1 };
            db.Users.AddRange(alice, bob);
            db.Books.Add(book);
            await db.SaveChangesAsync();

            aliceId = alice.Id;
            bobId = bob.Id;
            bookId = book.Id;

            var yearAgo = now.AddYears(-1);
            db.Borrowings.AddRange(
                new Borrowing { UserId = aliceId, BookId = bookId, BorrowedAt = now },
                new Borrowing { UserId = aliceId, BookId = bookId, BorrowedAt = now },
                new Borrowing { UserId = aliceId, BookId = bookId, BorrowedAt = now },
                new Borrowing { UserId = bobId, BookId = bookId, BorrowedAt = now },

                new Borrowing { UserId = bobId, BookId = bookId, BorrowedAt = yearAgo },
                new Borrowing { UserId = bobId, BookId = bookId, BorrowedAt = yearAgo },
                new Borrowing { UserId = bobId, BookId = bookId, BorrowedAt = yearAgo },
                new Borrowing { UserId = bobId, BookId = bookId, BorrowedAt = yearAgo },
                new Borrowing { UserId = bobId, BookId = bookId, BorrowedAt = yearAgo });
            await db.SaveChangesAsync();
        }

        var response = await fixture.ApiClient.GetAsync(
            $"/Users/top-borrowers?top=10&from={now.AddDays(-5).ToString("yyyy-MM-dd")}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var borrowers = JsonSerializer.Deserialize<List<TopBorrowerDto>>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(2, borrowers.Count);
        Assert.Equal("Alice", borrowers[0].FullName);
        Assert.Equal(3, borrowers[0].BorrowCount);
        Assert.Equal("Bob", borrowers[1].FullName);
        Assert.Equal(1, borrowers[1].BorrowCount);
    }

    [Fact]
    public async Task GetTopBorrowers_DateCanNotBeParsed_ReturnsBadRequest()
    {
        var now = DateTime.UtcNow;
        await using (var db = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var book = new Book { Title = "B", Author = "X", PageCount = 100, TotalCopies = 1 };
            db.Users.Add(alice);
            db.Books.Add(book);
            await db.SaveChangesAsync();

            db.Borrowings.AddRange(
                new Borrowing { UserId = alice.Id, BookId = book.Id, BorrowedAt = now });
            await db.SaveChangesAsync();
        }

        var response = await fixture.ApiClient.GetAsync(
            $"/Users/top-borrowers?top=10&from=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EstimateReadingPace_ReturnsAverage_FromHttpThroughGrpcToDb()
    {
        int aliceId;
        await using (var db = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var book = new Book { Title = "B", Author = "X", PageCount = 300, TotalCopies = 1 };
            db.Users.Add(alice);
            db.Books.Add(book);
            await db.SaveChangesAsync();
            aliceId = alice.Id;

            var start = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            db.Borrowings.Add(new Borrowing
            {
                UserId = alice.Id,
                BookId = book.Id,
                BorrowedAt = start,
                ReturnedAt = start.AddDays(10)
            });
            await db.SaveChangesAsync();
        }

        var response = await fixture.ApiClient.GetAsync($"/Users/{aliceId}/reading-pace");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pace = JsonSerializer.Deserialize<ReadingPaceDto>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(aliceId, pace.UserId);
        Assert.NotNull(pace.PagesPerDay);
        Assert.Equal(30.0, pace.PagesPerDay!.Value, 3);
    }
}
