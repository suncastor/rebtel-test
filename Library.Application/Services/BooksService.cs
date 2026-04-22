using Grpc.Core;
using Library.Contracts.V1;

namespace Library.Application.Services
{
    public class BooksService : Books.BooksBase
    {
        public override Task<GetMostBorrowedBooksResponse> GetMostBorrowedBooks(
            GetMostBorrowedBooksRequest request,
            ServerCallContext context)
        {
            var top = request.Top <= 0 ? 10 : Math.Min(request.Top, 100);

            var result = Enumerable.Range(1, top).Select(i => new BorrowedBook
            {
                BookId = i,
                BorrowCount = Random.Shared.Next(100, 1000),
                Title = "Book " + Guid.NewGuid(),
            });

            var response = new GetMostBorrowedBooksResponse();
            response.Books.AddRange(result);
            return Task.FromResult(response);
        }
    }
}