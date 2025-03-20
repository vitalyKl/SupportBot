using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TelegramEmailBot.Models;

namespace TelegramEmailBot.Services
{
    public class CompanyBindingService
    {
        private readonly string _filePath;
        private readonly Dictionary<long, string> _bindings = new Dictionary<long, string>();

        public CompanyBindingService(string filePath)
        {
            _filePath = filePath;
            LoadBindings();
        }

        private void LoadBindings()
        {
            if (!File.Exists(_filePath)) return;
            var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(';');
                if (parts.Length >= 2 && long.TryParse(parts[0], out long senderId))
                {
                    _bindings[senderId] = parts[1];
                }
            }
        }

        private void SaveBindings()
        {
            var lines = _bindings.Select(kvp => $"{kvp.Key};{kvp.Value}");
            File.WriteAllLines(_filePath, lines, Encoding.UTF8);
        }

        public bool TryGetCompany(long senderId, out string company)
        {
            return _bindings.TryGetValue(senderId, out company);
        }

        public void BindCompany(long senderId, string company)
        {
            _bindings[senderId] = company;
            SaveBindings();
        }
    }
}
