using System.ComponentModel.DataAnnotations;

namespace Cards.Application.Dtos;

public class CardReplaceDto
{
    [Required, MaxLength(200)]
    public string CardholderName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Nickname { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Brand { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{13,19}$", ErrorMessage = "cardNumber must contain 13 to 19 numeric digits.")]
    public string CardNumber { get; set; } = string.Empty;

    [Required]
    public DateOnly ExpirationDate { get; set; }

    [Required, Range(0, double.MaxValue, ErrorMessage = "creditLimit must be greater than or equal to zero.")]
    public decimal CreditLimit { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "pin must be exactly 4 numeric digits.")]
    public string Pin { get; set; } = string.Empty;
}
