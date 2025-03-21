using System;

namespace SupportBot.Core.Entities
{
    public class Message
    {
        public int Id { get; set; }
        // Инициализируем пустой строкой, чтобы не было предупреждения о null.
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public override string ToString()
        {
            return $"{Id},{Content},{CreatedAt}";
        }
    }
}
