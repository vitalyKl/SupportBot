using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SupportBot.Services.Security;
using TelegramEmailBot.Services.Security;

namespace TelegramEmailBot.Services
{
    public class CompanyListService
    {
        private readonly string _filePath;

        public CompanyListService(string filePath)
        {
            _filePath = filePath;
        }

        public IEnumerable<string> GetCompanies()
        {
            if (!File.Exists(_filePath))
                return Enumerable.Empty<string>();

            var companies = new List<string>();
            // Открываем файл с разрешением совместного доступа для чтения и записи другими процессами
            using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        string decrypted = EncryptionHelper.DecryptString(line, EncryptionSettings.EncryptionKey);
                        companies.Add(decrypted);
                    }
                    catch (Exception)
                    {
                        companies.Add(line.Trim());
                    }
                }
            }
            return companies;
        }

        public void AddCompany(string company)
        {
            if (string.IsNullOrWhiteSpace(company))
                return;

            string encrypted = EncryptionHelper.EncryptString(company, EncryptionSettings.EncryptionKey);
            // Записываем с разрешением для чтения другими процессами (если требуется, можно использовать File.AppendAllText с явными опциями)
            using (var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.WriteLine(encrypted);
            }
        }
    }
}
