using Cards.Application.Interfaces.Repositories;
using Cards.Domain.Entities;
using Cards.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cards.Infrastructure.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db)
    {
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) =>
        DbSet.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
}
