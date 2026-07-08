using CardsApi.Domain.Entities;

namespace CardsApi.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct);
}
