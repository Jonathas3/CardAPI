using CardsApi.Application.Interfaces.Repositories;
using CardsApi.Domain.Entities;
using CardsApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardsApi.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DbSet<User> _users;

    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
        _users = _db.Users;
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) =>
        _users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
}
