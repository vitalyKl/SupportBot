using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace TelegramEmailBot.Services
{
    /// <summary>
    /// Реализует хранение привязок (SenderID -> Название компании)
    /// в CSV‑файле. Каждый элемент хранится в виде: senderId;companyName
    /// </summary>
    public class CompanyBindingService
    {
        private readonly string _filePath;
        private readonly Dictionary<long, string> _bindings;
        private readonly object _lock = new object();

        /// <summary>
        /// При инициализации загружаются привязки из файла (если он существует).
        /// </summary>
        /// <param name="filePath">Путь к CSV-файлу (по умолчанию "company_bindings.csv")</param>
        public CompanyBindingService(string filePath = "company_bindings.csv")
        {
            _filePath = filePath;
            _bindings = new Dictionary<long, string>();
            LoadBindings();
        }

        /// <summary>
        /// Загружает привязки из файла.
        /// Формат каждой строки: senderId;companyName
        /// </summary>
        private void LoadBindings()
        {
            if (!File.Exists(_filePath))
                return;

            try
            {
                var lines = File.ReadAllLines(_filePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var parts = line.Split(';');
                    if (parts.Length < 2)
                        continue;
                    if (long.TryParse(parts[0].Trim(), out long senderId))
                    {
                        string company = parts[1].Trim();
                        lock (_lock)
                        {
                            _bindings[senderId] = company;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки привязок: {ex.Message}");
            }
        }

        /// <summary>
        /// Сохраняет текущие привязки в CSV‑файл.
        /// Пере-записывается весь файл.
        /// </summary>
        private void SaveBindings()
        {
            try
            {
                var lines = new List<string>();
                lock (_lock)
                {
                    foreach (var kvp in _bindings)
                    {
                        lines.Add($"{kvp.Key};{kvp.Value}");
                    }
                }
                File.WriteAllLines(_filePath, lines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения привязок: {ex.Message}");
            }
        }

        /// <summary>
        /// Пытается получить название компании для данного senderId.
        /// </summary>
        public bool TryGetCompany(long senderId, out string company)
        {
            lock (_lock)
            {
                return _bindings.TryGetValue(senderId, out company);
            }
        }

        /// <summary>
        /// Привязывает отправителя (senderId) к указанной компании и сохраняет изменения.
        /// </summary>
        public void BindCompany(long senderId, string company)
        {
            lock (_lock)
            {
                _bindings[senderId] = company;
                SaveBindings();
            }
        }
    }
}
