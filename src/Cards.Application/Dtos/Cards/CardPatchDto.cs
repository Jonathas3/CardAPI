using System.ComponentModel.DataAnnotations;

namespace Cards.Application.Dtos;

public class CardPatchDto
{
    [MaxLength(200)]
    public string? CardholderName { get; set; }

    [MaxLength(100)]
    public string? Nickname { get; set; }

    [MaxLength(50)]
    public string? Brand { get; set; }

    public string? CardNumber { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "creditLimit must be greater than or equal to zero.")]
    public decimal? CreditLimit { get; set; }

    public string? Status { get; set; }

    [RegularExpression(@"^\d{4}$", ErrorMessage = "pin must be exactly 4 numeric digits.")]
    public string? Pin { get; set; }
}
