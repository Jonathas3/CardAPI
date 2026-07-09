namespace Cards.Domain.Entities;

public class PinAccessLog
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public Guid UserId { get; set; }
    public DateTime AccessedAt { get; set; }
    public string? Ip { get; set; }
}
