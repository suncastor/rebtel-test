namespace Library.Infrastructure.Entities;

public class Borrowing
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int UserId { get; set; }
    public DateTime BorrowedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }

    public Book Book { get; set; } = null!;
    public User User { get; set; } = null!;
}
