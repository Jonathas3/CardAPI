using Cards.Application.Interfaces.Services;

namespace Cards.Infrastructure.Services;

public class BCryptPasswordHasher : IPasswordHasher
{
    public bool Verify(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
