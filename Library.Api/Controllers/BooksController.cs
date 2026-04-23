using Library.Contracts.V1;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class BooksController(
    ILogger<BooksController> logger,
    Books.BooksClient booksClient) : ControllerBase
{
    private readonly ILogger<BooksController> _logger = logger;

    [HttpGet("most-borrowed")]
    public async Task<IActionResult> GetMostBorrowed([FromQuery] int top = 10, CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching top {Top} most borrowed books", top);

        var response = await booksClient.GetMostBorrowedBooksAsync(
            new GetMostBorrowedBooksRequest { Top = top },
            cancellationToken: ct);

        _logger.LogInformation("Retrieved {Count} most borrowed books", response.Books.Count);

        return Ok(response.Books.Select(b => new
        {
            b.BookId,
            b.Title,
            b.BorrowCount
        }));
    }

    [HttpGet("{bookId:int}/co-borrowed")]
    public async Task<IActionResult> GetCoBorrowed(
        int bookId,
        [FromQuery] int top = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching top {Top} co-borrowed books for book {BookId}", top, bookId);

        var response = await booksClient.GetCoBorrowedBooksAsync(
            new GetCoBorrowedBooksRequest { BookId = bookId, Top = top },
            cancellationToken: ct);

        _logger.LogInformation(
            "Retrieved {Count} co-borrowed books for book {BookId}",
            response.Books.Count,
            bookId);

        return Ok(response.Books.Select(b => new
        {
            b.BookId,
            b.Title,
            b.BorrowCount
        }));
    }
}