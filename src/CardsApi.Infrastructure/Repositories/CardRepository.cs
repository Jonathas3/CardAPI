using CardsApi.Application.Interfaces.Repositories;
using CardsApi.Domain.Entities;
using CardsApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardsApi.Infrastructure.Repositories;

public class CardRepository : GenericRepository<Card>, ICardRepository
{
    private readonly DbSet<PinAccessLog> _pinAccessLogs;

    public CardRepository(AppDbContext db) : base(db)
    {
        _pinAccessLogs = db.PinAccessLogs;
    }

    public async Task<CardListResult> ListAsync(
        Guid userId, DateOnly? expirationFrom, DateOnly? expirationTo, int page, int pageSize, CancellationToken ct)
    {
        var query = DbSet.AsNoTracking().Where(c => c.UserId == userId && !c.IsDeleted);

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
        DbSet.FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId, ct);

    public Task AddPinAccessLogAsync(PinAccessLog log, CancellationToken ct)
    {
        _pinAccessLogs.Add(log);
        return Task.CompletedTask;
    }
}
