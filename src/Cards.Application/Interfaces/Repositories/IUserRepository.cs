using Cards.Domain.Entities;

namespace Cards.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct);
}
