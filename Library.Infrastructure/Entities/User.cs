namespace Library.Infrastructure.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public ICollection<Borrowing> Borrowings { get; set; } = new List<Borrowing>();
}
