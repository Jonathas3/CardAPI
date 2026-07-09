namespace Cards.Application.Dtos;

public class CardResponseDto
{
    public Guid Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string MaskedNumber { get; set; } = string.Empty;
    public DateOnly ExpirationDate { get; set; }
    public decimal CreditLimit { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
