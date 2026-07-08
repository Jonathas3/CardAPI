using CardsApi.Application.Interfaces.Services;

namespace CardsApi.Infrastructure.Services;

public class BCryptPasswordHasher : IPasswordHasher
{
    public bool Verify(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
