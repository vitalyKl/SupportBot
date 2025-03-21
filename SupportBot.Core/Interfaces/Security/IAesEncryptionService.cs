namespace SupportBot.Core.Interfaces.Security
{
    public interface IAesEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }
}
