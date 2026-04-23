using Library.Application.Services;
using Library.Infrastructure;
using Library.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (!app.Environment.IsEnvironment("Testing"))
    {
        await DataSeeder.SeedAsync(db);
    }
}

app.MapGrpcService<BooksService>();
app.MapGrpcService<UsersService>();
app.MapGet("/", () => "Library.Application gRPC host — use a gRPC client.");

app.Run();

public partial class Program;

namespace Library.Application
{
    public sealed class GrpcAppMarker;
}
