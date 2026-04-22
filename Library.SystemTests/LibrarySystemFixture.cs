using Library.Api;
using Library.Application;
using Library.Contracts.V1;
using Library.Infrastructure;
using Library.Infrastructure.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Library.SystemTests;

public class LibrarySystemFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("library_st")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebApplicationFactory<GrpcAppMarker> _grpcFactory = null!;
    private WebApplicationFactory<HttpApiMarker> _apiFactory = null!;

    public HttpClient ApiClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _grpcFactory = new WebApplicationFactory<GrpcAppMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _container.GetConnectionString(),
                    });
                });
            });

        var grpcHandler = _grpcFactory.Server.CreateHandler();

        _apiFactory = new WebApplicationFactory<HttpApiMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<Books.BooksClient>();
                    services.AddSingleton(_ =>
                    {
                        var channel = Grpc.Net.Client.GrpcChannel.ForAddress(
                            "http://localhost",
                            new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = grpcHandler });
                        return new Books.BooksClient(channel);
                    });
                });
            });

        ApiClient = _apiFactory.CreateClient();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        ApiClient?.Dispose();
        if (_apiFactory is not null)
        {
            await _apiFactory.DisposeAsync();
        }

        if (_grpcFactory is not null)
        {
            await _grpcFactory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"Borrowings\", \"Books\", \"Users\" RESTART IDENTITY CASCADE;");
    }

    public async Task SeedAsync(params (string Title, int BorrowCount)[] rows)
    {
        await using var db = CreateContext();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "reader@example.com");
        if (user is null)
        {
            user = new User { FullName = "Reader", Email = "reader@example.com" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        foreach (var (title, count) in rows)
        {
            var book = new Book { Title = title, Author = "A", PageCount = 100, TotalCopies = 1 };
            db.Books.Add(book);
            await db.SaveChangesAsync();

            for (int i = 0; i < count; i++)
            {
                db.Borrowings.Add(new Borrowing
                {
                    BookId = book.Id,
                    UserId = user.Id,
                    BorrowedAt = DateTime.UtcNow.AddMinutes(-i),
                });
            }
        }

        await db.SaveChangesAsync();
    }
}

[CollectionDefinition(nameof(LibrarySystemCollection))]
public class LibrarySystemCollection : ICollectionFixture<LibrarySystemFixture>
{
}
