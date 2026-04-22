using Library.Infrastructure.Entities;
using Library.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class UserRepositoryTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddAsync_PersistsUser()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserRepository(db);

        var user = new User { FullName = "Jane Doe", Email = "jane@example.com" };
        await repo.AddAsync(user);
        await repo.SaveChangesAsync();

        await using var verify = fixture.CreateContext();
        var saved = await verify.Users.SingleAsync();
        Assert.Equal("jane@example.com", saved.Email);
    }

    [Fact]
    public async Task Email_UniqueIndex_PreventsDuplicates()
    {
        await using (var seed = fixture.CreateContext())
        {
            seed.Users.Add(new User { FullName = "First", Email = "dup@example.com" });
            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var repo = new UserRepository(db);
        await repo.AddAsync(new User { FullName = "Second", Email = "dup@example.com" });

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.SaveChangesAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser_WhenExists()
    {
        int id;
        await using (var seed = fixture.CreateContext())
        {
            var user = new User { FullName = "Find Me", Email = "find@example.com" };
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            id = user.Id;
        }

        await using var db = fixture.CreateContext();
        var repo = new UserRepository(db);
        var result = await repo.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("find@example.com", result!.Email);
    }

    [Fact]
    public async Task Delete_RemovesUser()
    {
        int id;
        await using (var seed = fixture.CreateContext())
        {
            var user = new User { FullName = "Gone", Email = "gone@example.com" };
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            id = user.Id;
        }

        await using (var db = fixture.CreateContext())
        {
            var repo = new UserRepository(db);
            var user = await repo.GetByIdAsync(id);
            repo.Delete(user!);
            await repo.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        Assert.Null(await verify.Users.SingleOrDefaultAsync(u => u.Id == id));
    }
}
