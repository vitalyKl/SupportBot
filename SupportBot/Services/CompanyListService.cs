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
        private readonly object _lock = new();

        // Ключ шифрования из EncryptionSettings; в production лучше брать ключ из защищённого источника.
        private readonly string _encryptionKey = EncryptionSettings.EncryptionKey;

        public CompanyListService(string filePath = "companies.csv")
        {
            _filePath = filePath;
            // Если файла нет, создаем его с начальными данными (зашифрованными)
            if (!File.Exists(_filePath))
            {
                var defaultCompanies = new List<string> { "Компания А", "Компания Б", "Компания В" };
                var defaultEncrypted = defaultCompanies
                    .Select(c => EncryptionHelper.EncryptString(c, _encryptionKey))
                    .ToList();
                File.WriteAllLines(_filePath, defaultEncrypted, Encoding.UTF8);
            }
        }

        public List<string> GetCompanies()
        {
            try
            {
                lock (_lock)
                {
                    var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
                    var companies = new List<string>();
                    bool migrated = false;
                    foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
                    {
                        try
                        {
                            // Попытка дешифрования
                            string decrypted = EncryptionHelper.DecryptString(line, _encryptionKey);
                            companies.Add(decrypted);
                        }
                        catch (Exception)
                        {
                            // Если дешифрование не удалось, значит запись хранится в открытом виде
                            companies.Add(line.Trim());
                            migrated = true;
                        }
                    }
                    // Если обнаружены открытые записи, перезаписываем файл в зашифрованном виде
                    if (migrated)
                    {
                        SaveCompanies(companies);
                    }
                    return companies;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при чтении списка компаний: " + ex.Message);
                return new List<string>();
            }
        }

        private void SaveCompanies(List<string> companies)
        {
            try
            {
                var linesToWrite = companies
                    .Select(c => EncryptionHelper.EncryptString(c, _encryptionKey))
                    .ToList();
                File.WriteAllLines(_filePath, linesToWrite, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при сохранении списка компаний: " + ex.Message);
            }
        }

        public void AddCompany(string company)
        {
            if (string.IsNullOrWhiteSpace(company))
                return;
            lock (_lock)
            {
                var companies = GetCompanies();
                if (!companies.Contains(company, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        string encrypted = EncryptionHelper.EncryptString(company, _encryptionKey);
                        File.AppendAllLines(_filePath, new[] { encrypted }, Encoding.UTF8);
                        Console.WriteLine($"Компания '{company}' добавлена в список.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Ошибка при добавлении компании: " + ex.Message);
                    }
                }
            }
        }
    }
}
