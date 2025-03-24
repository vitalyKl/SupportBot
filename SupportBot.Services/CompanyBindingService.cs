using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;
using SupportBot.Core.Interfaces.Services;
using SupportBot.Services.Security;

namespace SupportBot.Services
{
    public class CompanyBindingService : ICompanyBindingService
    {
        private readonly string _filePath = "company_bindings.csv";
        private readonly string _encryptionKey;

        public CompanyBindingService(IOptions<Core.Configurations.BotSettings> options)
        {
            _encryptionKey = options.Value.EncryptionKey;
        }

        public string? GetBinding(string authorId)
        {
            if (!File.Exists(_filePath))
                return null;

            var lines = File.ReadAllLines(_filePath);
            foreach (var encryptedLine in lines)
            {
                // Проверяем, что строка не пустая
                if (string.IsNullOrWhiteSpace(encryptedLine))
                    continue;

                string line;
                try
                {
                    line = EncryptionHelper.DecryptString(encryptedLine, _encryptionKey);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Ошибка дешифрования в CompanyBindingService: {ex.Message}");
                    continue;
                }

                var parts = line.Split(',');
                if (parts.Length >= 2 && parts[0] == authorId)
                    return parts[1];
            }
            return null;
        }

        public void SaveBinding(string authorId, string companyName)
        {
            // Если привязка уже существует, ничего не делаем
            if (GetBinding(authorId) != null)
                return;

            var plainLine = $"{authorId},{companyName}";
            var encryptedLine = EncryptionHelper.EncryptString(plainLine, _encryptionKey);
            File.AppendAllLines(_filePath, new[] { encryptedLine });
        }
    }
}
