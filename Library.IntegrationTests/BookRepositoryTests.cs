using Library.Infrastructure.Entities;
using Library.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class BookRepositoryTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddAsync_PersistsBook()
    {
        await using var db = fixture.CreateContext();
        var repo = new BookRepository(db);

        var book = new Book { Title = "Dune", Author = "Frank Herbert", PageCount = 688, TotalCopies = 3 };
        await repo.AddAsync(book);
        await repo.SaveChangesAsync();

        await using var verify = fixture.CreateContext();
        var saved = await verify.Books.SingleAsync();
        Assert.Equal("Dune", saved.Title);
        Assert.Equal("Frank Herbert", saved.Author);
        Assert.True(saved.Id > 0);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingBook_ReturnsBook()
    {
        int id;
        await using (var seed = fixture.CreateContext())
        {
            var book = new Book { Title = "Hyperion", Author = "Dan Simmons", PageCount = 482, TotalCopies = 2 };
            seed.Books.Add(book);
            await seed.SaveChangesAsync();
            id = book.Id;
        }

        await using var db = fixture.CreateContext();
        var repo = new BookRepository(db);

        var result = await repo.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("Hyperion", result!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_OnMissingBook_ReturnsNull()
    {
        await using var db = fixture.CreateContext();
        var repo = new BookRepository(db);

        var result = await repo.GetByIdAsync(9999);

        Assert.Null(result);
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        int id;
        await using (var seed = fixture.CreateContext())
        {
            var book = new Book { Title = "Old Title", Author = "Author", PageCount = 100, TotalCopies = 1 };
            seed.Books.Add(book);
            await seed.SaveChangesAsync();
            id = book.Id;
        }

        await using (var db = fixture.CreateContext())
        {
            var repo = new BookRepository(db);
            var book = await repo.GetByIdAsync(id);
            book!.Title = "New Title";
            repo.Update(book);
            await repo.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var updated = await verify.Books.SingleAsync(b => b.Id == id);
        Assert.Equal("New Title", updated.Title);
    }

    [Fact]
    public async Task Delete_RemovesBookAndCascadesBorrowings()
    {
        int bookId;
        await using (var seed = fixture.CreateContext())
        {
            var user = new User { FullName = "U", Email = "u@test.com" };
            var book = new Book { Title = "Doomed", Author = "A", PageCount = 10, TotalCopies = 1 };
            seed.Users.Add(user);
            seed.Books.Add(book);
            await seed.SaveChangesAsync();

            seed.Borrowings.Add(new Borrowing
            {
                BookId = book.Id,
                UserId = user.Id,
                BorrowedAt = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();
            bookId = book.Id;
        }

        await using (var db = fixture.CreateContext())
        {
            var repo = new BookRepository(db);
            var book = await repo.GetByIdAsync(bookId);
            repo.Delete(book!);
            await repo.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        Assert.Null(await verify.Books.SingleOrDefaultAsync(b => b.Id == bookId));
        Assert.Empty(await verify.Borrowings.Where(b => b.BookId == bookId).ToListAsync());
    }

    [Fact]
    public async Task GetAll_ReturnsQueryableOverAllBooks()
    {
        await using (var seed = fixture.CreateContext())
        {
            seed.Books.AddRange(
                new Book { Title = "B1", Author = "A1", PageCount = 1, TotalCopies = 1 },
                new Book { Title = "B2", Author = "A2", PageCount = 2, TotalCopies = 2 },
                new Book { Title = "B3", Author = "A3", PageCount = 3, TotalCopies = 3 });
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var repo = new BookRepository(db);

        var titles = await repo.GetAll().OrderBy(b => b.Title).Select(b => b.Title).ToListAsync();

        Assert.Equal(new[] { "B1", "B2", "B3" }, titles);
    }

    [Fact]
    public async Task BookTitle_ExceedingMaxLength_Throws()
    {
        await using var db = fixture.CreateContext();
        var repo = new BookRepository(db);

        var book = new Book
        {
            Title = new string('x', 501),
            Author = "A",
            PageCount = 1,
            TotalCopies = 1
        };
        await repo.AddAsync(book);

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.SaveChangesAsync());
    }
}
