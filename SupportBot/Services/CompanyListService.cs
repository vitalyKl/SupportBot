using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TelegramEmailBot.Services
{
    public class CompanyListService
    {
        private readonly string _filePath;
        private readonly object _lock = new object();

        public CompanyListService(string filePath = "companies.csv")
        {
            _filePath = filePath;
            // Если файла нет, создаём его с начальными значениями
            if (!File.Exists(_filePath))
            {
                var defaultCompanies = new List<string> { };
                File.WriteAllLines(_filePath, defaultCompanies, Encoding.UTF8);
            }
        }

        public List<string> GetCompanies()
        {
            try
            {
                lock (_lock)
                {
                    var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
                    return lines
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim())
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при чтении списка компаний: " + ex.Message);
                return new List<string>();
            }
        }

        public void AddCompany(string company)
        {
            if (string.IsNullOrWhiteSpace(company))
                return;
            lock (_lock)
            {
                // Получаем текущий список компаний
                var companies = GetCompanies();
                // Если такой компании ещё нет (игнорируя регистр)
                if (!companies.Contains(company, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Дописываем новую компанию в файл
                        File.AppendAllLines(_filePath, new string[] { company }, Encoding.UTF8);
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
