using Library.Infrastructure.Entities;

namespace Library.Infrastructure.Repositories;

public class BorrowingRepository(AppDbContext context) : BaseRepository<Borrowing>(context), IBorrowingRepository
{
}
