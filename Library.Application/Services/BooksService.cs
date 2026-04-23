using Grpc.Core;
using Library.Contracts.V1;
using Library.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

public class BooksService(
    ILogger<BooksService> logger,
    IBorrowingRepository borrowingRepository) : Books.BooksBase
{
    public override async Task<GetMostBorrowedBooksResponse> GetMostBorrowedBooks(
        GetMostBorrowedBooksRequest request,
        ServerCallContext context)
    {
        var top = request.Top <= 0 ? 10 : Math.Min(request.Top, 100);

        logger.LogInformation("Computing top {Top} most borrowed books", top);

        var books = await borrowingRepository.GetAll()
            .Include(b => b.Book)
            .GroupBy(b => new { b.BookId, b.Book.Title })
            .Select(g => new
            {
                g.Key.BookId,
                g.Key.Title,
                BorrowCount = g.Count()
            })
            .OrderByDescending(s => s.BorrowCount)
            .Take(top)
            .ToListAsync();

        logger.LogInformation("Returning {Count} most borrowed books", books.Count);

        var response = new GetMostBorrowedBooksResponse();
        response.Books.AddRange(books.Select(s => new BorrowedBook
        {
            BookId = s.BookId,
            Title = s.Title,
            BorrowCount = s.BorrowCount
        }));
        return response;
    }
}
