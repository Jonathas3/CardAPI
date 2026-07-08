using CardsApi.Application.Interfaces.Repositories;
using CardsApi.Domain.Entities;
using CardsApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardsApi.Infrastructure.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db)
    {
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) =>
        DbSet.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
}
