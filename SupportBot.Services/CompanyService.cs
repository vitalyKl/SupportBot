using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;
using SupportBot.Core.Interfaces.Services;
using SupportBot.Services.Security;

namespace SupportBot.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly string _filePath = "companies.csv";
        private readonly string _encryptionKey;

        public CompanyService(IOptions<Core.Configurations.BotSettings> options)
        {
            _encryptionKey = options.Value.EncryptionKey;
        }

        public IEnumerable<string> GetAllCompanies()
        {
            if (!File.Exists(_filePath))
                return Enumerable.Empty<string>();

            var lines = File.ReadAllLines(_filePath);
            var companies = new List<string>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                try
                {
                    var decrypted = EncryptionHelper.DecryptString(line, _encryptionKey);
                    if (!string.IsNullOrWhiteSpace(decrypted))
                        companies.Add(decrypted);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Ошибка при дешифровке строки в companies.csv: {ex.Message}");
                }
            }
            return companies;
        }

        public IEnumerable<string> GetCompaniesByLetter(char letter)
        {
            var companies = GetAllCompanies();
            return companies.Where(c => char.ToUpper(c[0]) == char.ToUpper(letter));
        }

        public void AddCompanyIfNotExists(string companyName)
        {
            var companies = GetAllCompanies();
            if (companies.Contains(companyName))
                return;

            var encrypted = EncryptionHelper.EncryptString(companyName, _encryptionKey);
            File.AppendAllLines(_filePath, new[] { encrypted });
        }
    }
}
