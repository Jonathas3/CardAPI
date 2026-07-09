namespace Cards.Domain.Entities;

public class Card
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string CardholderName { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;

    public string CardNumberEncrypted { get; set; } = string.Empty;

    public string CardNumberFirst4 { get; set; } = string.Empty;

    public string CardNumberLast4 { get; set; } = string.Empty;

    public string PinEncrypted { get; set; } = string.Empty;

    public DateOnly ExpirationDate { get; set; }
    public decimal CreditLimit { get; set; }

    public CardStatus Status { get; set; } = CardStatus.Active;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public User? User { get; set; }

    public string MaskedNumber => $"{CardNumberFirst4} **** **** {CardNumberLast4}";

    public void UpdateDetails(
        string cardholderName,
        string nickname,
        string brand,
        string cardNumberEncrypted,
        string cardNumberFirst4,
        string cardNumberLast4,
        string pinEncrypted,
        DateOnly expirationDate,
        decimal creditLimit,
        CardStatus status,
        DateTime updatedAtUtc)
    {
        CardholderName = cardholderName;
        Nickname = nickname;
        Brand = brand;
        CardNumberEncrypted = cardNumberEncrypted;
        CardNumberFirst4 = cardNumberFirst4;
        CardNumberLast4 = cardNumberLast4;
        PinEncrypted = pinEncrypted;
        ExpirationDate = expirationDate;
        CreditLimit = creditLimit;
        Status = status;
        UpdatedAt = updatedAtUtc;
    }

    public void UpdateCardNumber(string encrypted, string first4, string last4, DateTime updatedAtUtc)
    {
        CardNumberEncrypted = encrypted;
        CardNumberFirst4 = first4;
        CardNumberLast4 = last4;
        UpdatedAt = updatedAtUtc;
    }

    public void UpdatePin(string encrypted, DateTime updatedAtUtc)
    {
        PinEncrypted = encrypted;
        UpdatedAt = updatedAtUtc;
    }

    public void SoftDelete(DateTime deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAt = deletedAtUtc;
        UpdatedAt = deletedAtUtc;
    }

}
