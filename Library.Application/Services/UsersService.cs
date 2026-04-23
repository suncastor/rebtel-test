using Grpc.Core;
using Library.Contracts.V1;
using Library.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

public class UsersService(
    ILogger<UsersService> logger,
    IBorrowingRepository borrowingRepository) : Users.UsersBase
{
    public override async Task<GetTopBorrowersResponse> GetTopBorrowers(
        GetTopBorrowersRequest request,
        ServerCallContext context)
    {
        var top = request.Top <= 0 ? 10 : Math.Min(request.Top, 100);

        logger.LogInformation(
            "Computing top {Top} borrowers from {From} to {To}",
            top,
            request.From?.ToDateTime(),
            request.To?.ToDateTime());

        var query = borrowingRepository.GetAll()
            .Include(b => b.User)
            .AsQueryable();
        if (request.From is not null)
        {
            var from = request.From.ToDateTime();
            query = query.Where(b => b.BorrowedAt >= from);
        }

        if (request.To is not null)
        {
            var to = request.To.ToDateTime();
            query = query.Where(b => b.BorrowedAt <= to);
        }

        var borrowers = await query
            .GroupBy(b => new { b.UserId, b.User.FullName })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.FullName,
                BorrowCount = g.Count()
            })
            .OrderByDescending(s => s.BorrowCount)
            .Take(top)
            .ToListAsync();

        logger.LogInformation("Returning {Count} top borrowers", borrowers.Count);

        var response = new GetTopBorrowersResponse();
        response.Borrowers.AddRange(borrowers.Select(b => new TopBorrower
        {
            UserId = b.UserId,
            FullName = b.FullName,
            BorrowCount = b.BorrowCount
        }));
        return response;
    }

    public override async Task<EstimateReadingPaceResponse> EstimateReadingPace(
        EstimateReadingPaceRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("Estimating reading pace for user {UserId}", request.UserId);
        var borrowedBooks = await borrowingRepository.GetAll()
            .Where(b => b.UserId == request.UserId && b.ReturnedAt != null)
            .Include(b => b.Book)
            .Select(b => new
            {
                b.Book.PageCount,
                b.BorrowedAt,
                ReturnedAt = b.ReturnedAt!.Value
            })
            .ToListAsync();

        var bookReadingDays = borrowedBooks
            .Select(r => new { r.PageCount, Days = (int)(r.ReturnedAt - r.BorrowedAt).TotalDays })
            .Where(x => x.Days > 0)
            .ToList();

        var response = new EstimateReadingPaceResponse
        {
            UserId = request.UserId,
        };

        if (bookReadingDays.Count > 0)
        {
            response.PagesPerDay = bookReadingDays.Average(x => (double)x.PageCount / x.Days);
        }

        logger.LogInformation(
            "Reading pace for user {UserId}: {PagesPerDay} pages/day",
            response.UserId,
            response.HasPagesPerDay ? response.PagesPerDay : (double?)null);
        return response;
    }
}
