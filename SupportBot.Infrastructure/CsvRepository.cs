using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SupportBot.Core.Interfaces.Data;

namespace SupportBot.Infrastructure
{
    public class CsvRepository<T> : ICsvRepository<T>
    {
        private readonly string _filePath;

        public CsvRepository()
        {
            _filePath = $"{typeof(T).Name}.csv";
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            if (!File.Exists(_filePath))
                return Enumerable.Empty<T>();

            var lines = await File.ReadAllLinesAsync(_filePath);
            // Если реализация парсинга отсутствует, можно вернуть строку, преобразованную к значению по умолчанию.
            // Добавляем оператор !, чтобы подавить предупреждение о возможном null.
            return lines.Select(line => default(T)!);
        }

        public async Task SaveAsync(T item)
        {
            var csvLine = item?.ToString();
            await File.AppendAllTextAsync(_filePath, csvLine + Environment.NewLine);
        }
    }
}
