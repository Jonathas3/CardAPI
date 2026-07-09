namespace Cards.Application.Interfaces.Services;

public interface ICryptoService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherTextBase64);
}
