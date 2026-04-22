using Library.Contracts.V1;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers
{
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
            var response = await booksClient.GetMostBorrowedBooksAsync(
                new GetMostBorrowedBooksRequest { Top = top },
                cancellationToken: ct);

            return Ok(response.Books.Select(b => new
            {
                b.BookId,
                b.Title,
                b.BorrowCount
            }));
        }
    }
}