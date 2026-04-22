using Library.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Seeding;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Books.AnyAsync(cancellationToken)) return;

        var rng = new Random();

        var books = new[]
        {
            new Book {
                Title = "Moby Dick",
                Author = "Herman Melville",
                PageCount = 635,
                TotalCopies = 5
            },
            new Book {
                Title = "1984",
                Author = "George Orwell",
                PageCount = 328,
                TotalCopies = 7
            },
            new Book {
                Title = "To Kill a Mockingbird",
                Author = "Harper Lee",
                PageCount = 281,
                TotalCopies = 4
            },
            new Book {
                Title = "The Great Gatsby",
                Author = "F. Scott Fitzgerald",
                PageCount = 180,
                TotalCopies = 6
            },
            new Book {
                Title = "Brave New World",
                Author = "Aldous Huxley",
                PageCount = 311,
                TotalCopies = 3
            },
            new Book {
                Title = "Pride and Prejudice",
                Author = "Jane Austen",
                PageCount = 432,
                TotalCopies = 5
            },
            new Book {
                Title = "The Catcher in the Rye",
                Author = "J.D. Salinger",
                PageCount = 277,
                TotalCopies = 4
            },
            new Book {
                Title = "Fahrenheit 451",
                Author = "Ray Bradbury",
                PageCount = 249,
                TotalCopies = 3
            },
        };

        var users = new[]
        {
            new User { FullName = "Alice Johnson", Email = "alice@example.com" },
            new User { FullName = "Bob Smith", Email = "bob@example.com" },
            new User { FullName = "Carol Davis", Email = "carol@example.com" },
            new User { FullName = "David Wilson", Email = "david@example.com" },
            new User { FullName = "Eve Martinez", Email = "eve@example.com" },
        };

        context.Books.AddRange(books);
        context.Users.AddRange(users);
        await context.SaveChangesAsync(cancellationToken);

        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var borrowings = new List<Borrowing>();

        for (int i = 0; i < 120; i++)
        {
            var book = books[rng.Next(books.Length)];
            var user = users[rng.Next(users.Length)];
            var borrowedAt = baseDate.AddDays(rng.Next(0, 110));
            DateTime? returnedAt = rng.NextDouble() < 0.8
                ? borrowedAt.AddDays(rng.Next(3, 30))
                : null;

            borrowings.Add(new Borrowing
            {
                BookId = book.Id,
                UserId = user.Id,
                BorrowedAt = borrowedAt,
                ReturnedAt = returnedAt
            });
        }

        context.Borrowings.AddRange(borrowings);
        await context.SaveChangesAsync(cancellationToken);
    }
}
