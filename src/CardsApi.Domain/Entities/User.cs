namespace CardsApi.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<Card> Cards { get; set; } = new List<Card>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
