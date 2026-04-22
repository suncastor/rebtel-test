using Library.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<BooksService>();
app.MapGet("/", () => "Library.Application gRPC host — use a gRPC client.");

app.Run();