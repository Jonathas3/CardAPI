using Cards.Application.Common;
using Cards.Application.Dtos;
using Cards.Application.Interfaces.Repositories;
using Cards.Application.Interfaces.Services;
using Cards.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Cards.Application.Services;

public class CardService : ICardService
{
    private const int PageSize = 10;

    private readonly ICardRepository _cards;
    private readonly ICryptoService _crypto;
    private readonly ILogger<CardService> _logger;

    public CardService(ICardRepository cards, ICryptoService crypto, ILogger<CardService> logger)
    {
        _cards = cards;
        _crypto = crypto;
        _logger = logger;
    }

    public async Task<PagedResultDto<CardResponseDto>> ListAsync(
        Guid userId, int page, DateOnly? expirationFrom, DateOnly? expirationTo, CancellationToken ct)
    {
        if (page < 1) page = 1;

        if (expirationFrom is not null && expirationTo is not null && expirationFrom > expirationTo)
        {
            throw ApiException.BadRequest("expirationFrom must be less than or equal to expirationTo.");
        }

        var listResult = await _cards.ListAsync(userId, expirationFrom, expirationTo, page, PageSize, ct);
        var totalPages = listResult.TotalCount == 0 ? 0 : (int)Math.Ceiling(listResult.TotalCount / (double)PageSize);

        var items = listResult.Items.Select(ToResponseDto).ToList();

        return new PagedResultDto<CardResponseDto>
        {
            Page = page,
            PageSize = PageSize,
            TotalItems = listResult.TotalCount,
            TotalPages = totalPages,
            Items = items
        };
    }

    public async Task<CardResponseDto> GetByIdAsync(Guid userId, Guid cardId, CancellationToken ct)
    {
        var card = await FindOwnedCardAsync(userId, cardId, ct);
        return ToResponseDto(card);
    }

    public async Task<CardResponseDto> CreateAsync(Guid userId, CardCreateDto dto, CancellationToken ct)
    {
        var status = ParseStatusOrDefault(dto.Status);

        var digits = dto.CardNumber.Trim();
        var now = DateTime.UtcNow;

        var card = new Card
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CardholderName = dto.CardholderName.Trim(),
            Nickname = dto.Nickname.Trim(),
            Brand = dto.Brand.Trim().ToUpperInvariant(),
            CardNumberEncrypted = _crypto.Encrypt(digits),
            CardNumberFirst4 = digits[..4],
            CardNumberLast4 = digits[^4..],
            PinEncrypted = _crypto.Encrypt(dto.Pin),
            ExpirationDate = dto.ExpirationDate,
            CreditLimit = dto.CreditLimit,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        await _cards.AddAsync(card, ct);
        await _cards.SaveChangesAsync(ct);

        _logger.LogInformation("Card {CardId} created for user {UserId}", card.Id, userId);

        return ToResponseDto(card);
    }

    public async Task<CardResponseDto> ReplaceAsync(Guid userId, Guid cardId, CardReplaceDto dto, CancellationToken ct)
    {
        var card = await FindOwnedCardAsync(userId, cardId, ct);

        var digits = dto.CardNumber.Trim();

        card.UpdateDetails(
            dto.CardholderName.Trim(),
            dto.Nickname.Trim(),
            dto.Brand.Trim().ToUpperInvariant(),
            _crypto.Encrypt(digits),
            digits[..4],
            digits[^4..],
            _crypto.Encrypt(dto.Pin),
            dto.ExpirationDate,
            dto.CreditLimit,
            ParseStatus(dto.Status),
            DateTime.UtcNow);
        await _cards.SaveChangesAsync(ct);

        _logger.LogInformation("Card {CardId} fully updated for user {UserId}", card.Id, userId);

        return ToResponseDto(card);
    }

    public async Task<CardResponseDto> PatchAsync(Guid userId, Guid cardId, CardPatchDto dto, CancellationToken ct)
    {
        var card = await FindOwnedCardAsync(userId, cardId, ct);

        if (dto.CardholderName is not null) card.CardholderName = dto.CardholderName.Trim();
        if (dto.Nickname is not null) card.Nickname = dto.Nickname.Trim();
        if (dto.Brand is not null) card.Brand = dto.Brand.Trim().ToUpperInvariant();

        if (dto.CardNumber is not null)
        {
            var digits = dto.CardNumber.Trim();
            if (digits.Length < 13 || digits.Length > 19 || !digits.All(char.IsDigit))
            {
                throw ApiException.BadRequest("cardNumber must contain 13 to 19 numeric digits.");
            }

            card.UpdateCardNumber(_crypto.Encrypt(digits), digits[..4], digits[^4..], DateTime.UtcNow);
        }

        if (dto.ExpirationDate is not null) card.ExpirationDate = dto.ExpirationDate.Value;
        if (dto.CreditLimit is not null) card.CreditLimit = dto.CreditLimit.Value;

        if (dto.Status is not null)
        {
            card.Status = ParseStatus(dto.Status);
        }

        if (dto.Pin is not null) card.UpdatePin(_crypto.Encrypt(dto.Pin), DateTime.UtcNow);

        card.UpdatedAt = DateTime.UtcNow;

        await _cards.SaveChangesAsync(ct);

        _logger.LogInformation("Card {CardId} partially updated for user {UserId}", card.Id, userId);

        return ToResponseDto(card);
    }

    public async Task<DeleteResultDto> DeleteAsync(Guid userId, Guid cardId, CancellationToken ct)
    {
        var card = await FindOwnedCardAsync(userId, cardId, ct);

        card.SoftDelete(DateTime.UtcNow);

        await _cards.SaveChangesAsync(ct);

        _logger.LogInformation("Card {CardId} deleted (soft) for user {UserId}", card.Id, userId);

        return new DeleteResultDto
        {
            Id = card.Id,
            Deleted = true,
            Message = "Card removed successfully."
        };
    }

    public async Task<CardPinResponseDto> GetPinAsync(Guid userId, Guid cardId, string? requestIp, CancellationToken ct)
    {
        var card = await FindOwnedCardAsync(userId, cardId, ct);
        var pin = _crypto.Decrypt(card.PinEncrypted);

        await _cards.AddPinAccessLogAsync(new PinAccessLog
        {
            Id = Guid.NewGuid(),
            CardId = card.Id,
            UserId = userId,
            AccessedAt = DateTime.UtcNow,
            Ip = requestIp
        }, ct);
        await _cards.SaveChangesAsync(ct);

        _logger.LogInformation("PIN accessed for card {CardId} by user {UserId}", card.Id, userId);

        return new CardPinResponseDto
        {
            CardId = card.Id,
            Pin = pin,
            RetrievedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<Card> FindOwnedCardAsync(Guid userId, Guid cardId, CancellationToken ct)
    {
        var card = await _cards.FindByIdAsync(userId, cardId, ct);
        return card ?? throw ApiException.NotFound("Card not found.");
    }

    private static CardResponseDto ToResponseDto(Card c) => new()
    {
        Id = c.Id,
        Nickname = c.Nickname,
        Brand = c.Brand,
        MaskedNumber = $"{c.CardNumberFirst4} **** **** {c.CardNumberLast4}",
        ExpirationDate = c.ExpirationDate,
        CreditLimit = c.CreditLimit,
        Status = ToApiStatus(c.Status),
        CreatedAt = c.CreatedAt
    };

    private static CardStatus ParseStatusOrDefault(string? status)
    {
        return string.IsNullOrWhiteSpace(status) ? CardStatus.Active : ParseStatus(status);
    }

    private static CardStatus ParseStatus(string status)
    {
        return Enum.TryParse<CardStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : throw ApiException.BadRequest("status must be one of: ACTIVE, BLOCKED, CANCELLED.");
    }

    private static string ToApiStatus(CardStatus status)
    {
        return status.ToString().ToUpperInvariant();
    }
}
