using Library.Infrastructure.Entities;

namespace Library.Infrastructure.Repositories;

public class BookRepository(AppDbContext context) : BaseRepository<Book>(context), IBookRepository
{
}
