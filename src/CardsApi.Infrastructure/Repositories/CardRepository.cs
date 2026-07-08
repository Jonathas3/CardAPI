using CardsApi.Application.Interfaces.Repositories;
using CardsApi.Domain.Entities;
using CardsApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardsApi.Infrastructure.Repositories;

public class CardRepository : ICardRepository
{
    private readonly DbSet<Card> _cards;
    private readonly DbSet<PinAccessLog> _pinAccessLogs;
    private readonly AppDbContext _db;

    public CardRepository(AppDbContext db)
    {
        _db = db;
        _cards = db.Cards;
        _pinAccessLogs = db.PinAccessLogs;
    }

    public async Task<CardListResult> ListAsync(
        Guid userId, DateOnly? expirationFrom, DateOnly? expirationTo, int page, int pageSize, CancellationToken ct)
    {
        var query = _cards.AsNoTracking().Where(c => c.UserId == userId && !c.IsDeleted);

        if (expirationFrom is not null)
        {
            query = query.Where(c => c.ExpirationDate >= expirationFrom.Value);
        }

        if (expirationTo is not null)
        {
            query = query.Where(c => c.ExpirationDate <= expirationTo.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new CardListResult(items, totalCount);
    }

    public Task<Card?> FindByIdAsync(Guid userId, Guid cardId, CancellationToken ct) =>
        _cards.FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId, ct);

    public Task AddAsync(Card card, CancellationToken ct)
    {
        _cards.Add(card);
        return Task.CompletedTask;
    }

    public Task AddPinAccessLogAsync(PinAccessLog log, CancellationToken ct)
    {
        _pinAccessLogs.Add(log);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
