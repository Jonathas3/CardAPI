using Cards.Domain.Entities;

namespace Cards.Application.Interfaces.Repositories;

public interface ICardRepository
{
    Task<CardListResult> ListAsync(
        Guid userId, DateOnly? expirationFrom, DateOnly? expirationTo, int page, int pageSize, CancellationToken ct);

    Task<Card?> FindByIdAsync(Guid userId, Guid cardId, CancellationToken ct);

    Task AddAsync(Card card, CancellationToken ct);

    Task AddPinAccessLogAsync(PinAccessLog log, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
