namespace Cards.Application.Dtos;

public class CardPinResponseDto
{
    public Guid CardId { get; set; }
    public string Pin { get; set; } = string.Empty;
    public DateTime RetrievedAtUtc { get; set; }
}
