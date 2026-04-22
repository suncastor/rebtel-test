using System.Net;
using System.Text.Json;

namespace Library.SystemTests;

[Collection(nameof(LibrarySystemCollection))]
public class MostBorrowedEndpointTests(LibrarySystemFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private record BookDto(int BookId, string Title, int BorrowCount);

    [Fact]
    public async Task GetMostBorrowed_ReturnsRankedResults_FromHttpThroughGrpcToDb()
    {
        await fixture.SeedAsync(
            ("Moby Dick", 4),
            ("1984", 7),
            ("Dune", 2));

        var response = await fixture.ApiClient.GetAsync("/Books/most-borrowed?top=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var books = JsonSerializer.Deserialize<List<BookDto>>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(3, books.Count);
        Assert.Equal("1984", books[0].Title);
        Assert.Equal(7, books[0].BorrowCount);
        Assert.Equal("Moby Dick", books[1].Title);
        Assert.Equal("Dune", books[2].Title);
    }
}
