using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Library.Contracts.V1;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UsersController(
    ILogger<UsersController> logger,
    Users.UsersClient usersClient) : ControllerBase
{
    private readonly ILogger<UsersController> _logger = logger;

    [HttpGet("top-borrowers")]
    public async Task<IActionResult> GetTopBorrowers(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int top = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching top {Top} borrowers from {From} to {To}", top, from, to);
        if (!TryParseDate(from, out var fromUtc))
        {
            return BadRequest($"Invalid '{nameof(from)}' value. Expected ISO 8601 (e.g. 2026-01-01).");
        }

        if (!TryParseDate(to, out var toUtc))
        {
            return BadRequest($"Invalid '{nameof(to)}' value. Expected ISO 8601 (e.g. 2026-01-01).");
        }

        var request = new GetTopBorrowersRequest { Top = top };
        if (fromUtc.HasValue)
        {
            request.From = Timestamp.FromDateTime(fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            request.To = Timestamp.FromDateTime(toUtc.Value);
        }

        var response = await usersClient.GetTopBorrowersAsync(request, cancellationToken: ct);

        _logger.LogInformation("Retrieved {Count} top borrowers", response.Borrowers.Count);
        return Ok(response.Borrowers.Select(b => new
        {
            b.UserId,
            b.FullName,
            b.BorrowCount
        }));
    }

    [HttpGet("{userId:int}/reading-pace")]
    public async Task<IActionResult> EstimateReadingPace(int userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Estimating reading pace for user {UserId}", userId);
        var response = await usersClient.EstimateReadingPaceAsync(
            new EstimateReadingPaceRequest { UserId = userId },
            cancellationToken: ct);

        _logger.LogInformation(
            "Estimated reading pace for user {UserId}: {PagesPerDay} pages/day",
            response.UserId,
            response.HasPagesPerDay ? response.PagesPerDay : (double?)null);
        return Ok(new
        {
            response.UserId,
            PagesPerDay = response.HasPagesPerDay ? response.PagesPerDay : (double?)null,
        });
    }

    private static bool TryParseDate(string? value, out DateTime? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }

        if (DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            result = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        result = null;
        return false;
    }
}
