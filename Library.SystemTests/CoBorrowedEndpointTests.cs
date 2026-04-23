using System.Net;
using System.Text.Json;
using Library.Infrastructure.Entities;

namespace Library.SystemTests;

[Collection(nameof(LibrarySystemCollection))]
public class CoBorrowedEndpointTests(LibrarySystemFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private record CoBorrowedBookDto(int BookId, string Title, int BorrowCount);

    [Fact]
    public async Task GetCoBorrowed_ReturnsRankedResults_FromHttpThroughGrpcToDb()
    {
        int targetId;
        await using (var db = fixture.CreateContext())
        {
            var alice = new User { FullName = "Alice", Email = "alice@example.com" };
            var bob = new User { FullName = "Bob", Email = "bob@example.com" };
            var carol = new User { FullName = "Carol", Email = "carol@example.com" };
            db.Users.AddRange(alice, bob, carol);

            var target = new Book { Title = "Target", Author = "A", PageCount = 100, TotalCopies = 1 };
            var popular = new Book { Title = "Popular", Author = "A", PageCount = 100, TotalCopies = 1 };
            var niche = new Book { Title = "Niche", Author = "A", PageCount = 100, TotalCopies = 1 };
            var unrelated = new Book { Title = "Unrelated", Author = "A", PageCount = 100, TotalCopies = 1 };
            db.Books.AddRange(target, popular, niche, unrelated);
            await db.SaveChangesAsync();
            targetId = target.Id;

            var now = DateTime.UtcNow;
            db.Borrowings.AddRange(
                new Borrowing { UserId = alice.Id, BookId = target.Id, BorrowedAt = now },
                new Borrowing { UserId = bob.Id, BookId = target.Id, BorrowedAt = now },
                new Borrowing { UserId = alice.Id, BookId = popular.Id, BorrowedAt = now },
                new Borrowing { UserId = alice.Id, BookId = popular.Id, BorrowedAt = now },
                new Borrowing { UserId = bob.Id, BookId = popular.Id, BorrowedAt = now },
                new Borrowing { UserId = bob.Id, BookId = niche.Id, BorrowedAt = now },
                new Borrowing { UserId = carol.Id, BookId = unrelated.Id, BorrowedAt = now });
            await db.SaveChangesAsync();
        }

        var response = await fixture.ApiClient.GetAsync($"/Books/{targetId}/co-borrowed?top=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var books = JsonSerializer.Deserialize<List<CoBorrowedBookDto>>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(2, books.Count);
        Assert.Equal("Popular", books[0].Title);
        Assert.Equal(3, books[0].BorrowCount);
        Assert.Equal("Niche", books[1].Title);
        Assert.Equal(1, books[1].BorrowCount);
    }

    [Fact]
    public async Task GetCoBorrowed_TargetBookWasNeverBorrowed_ReturnsEmpty()
    {
        int targetId;
        await using (var db = fixture.CreateContext())
        {
            var target = new Book { Title = "Lonely", Author = "A", PageCount = 100, TotalCopies = 1 };
            db.Books.Add(target);
            await db.SaveChangesAsync();
            targetId = target.Id;
        }

        var response = await fixture.ApiClient.GetAsync($"/Books/{targetId}/co-borrowed?top=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var books = JsonSerializer.Deserialize<List<CoBorrowedBookDto>>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Empty(books);
    }
}
