using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SupportBot.Services.Security;
using TelegramEmailBot.Services.Security;

namespace TelegramEmailBot.Services
{
    public class CompanyBindingService
    {
        private readonly string _filePath;
        private readonly Dictionary<long, string> _bindings = new();
        private readonly object _lock = new();
        private readonly string _encryptionKey = EncryptionSettings.EncryptionKey;

        public CompanyBindingService(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            LoadBindings();
        }

        private void LoadBindings()
        {
            if (!File.Exists(_filePath))
                return;

            try
            {
                var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
                bool migrated = false;
                foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    var parts = line.Split(';');
                    if (parts.Length < 2)
                        continue;
                    if (long.TryParse(parts[0].Trim(), out long senderId))
                    {
                        string company;
                        try
                        {
                            company = EncryptionHelper.DecryptString(parts[1].Trim(), _encryptionKey);
                        }
                        catch (Exception)
                        {
                            company = parts[1].Trim();
                            migrated = true;
                        }
                        lock (_lock)
                        {
                            _bindings[senderId] = company;
                        }
                    }
                }
                if (migrated)
                {
                    SaveBindings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки привязок: {ex.Message}");
            }
        }

        private void SaveBindings()
        {
            try
            {
                var lines = new List<string>();
                lock (_lock)
                {
                    foreach (var kvp in _bindings)
                    {
                        string encrypted = EncryptionHelper.EncryptString(kvp.Value, _encryptionKey);
                        lines.Add($"{kvp.Key};{encrypted}");
                    }
                }
                File.WriteAllLines(_filePath, lines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения привязок: {ex.Message}");
            }
        }

        public bool TryGetCompany(long senderId, out string company)
        {
            lock (_lock)
            {
                return _bindings.TryGetValue(senderId, out company);
            }
        }

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
