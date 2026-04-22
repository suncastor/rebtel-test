namespace Library.Infrastructure.Entities;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int TotalCopies { get; set; }

    public ICollection<Borrowing> Borrowings { get; set; } = new List<Borrowing>();
}
