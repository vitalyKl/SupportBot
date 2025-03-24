using System;
using System.Collections.Generic;

namespace SupportBot.Core.Entities
{
    public class EmailMessage
    {
        public string SenderFirstName { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderUserName { get; set; } = string.Empty;
        public DateTime FirstMessageDate { get; set; }
        // Если установлена привязка – здесь будет название компании
        public string BoundCompany { get; set; } = string.Empty;
        // Список блоков, которые представляют каждое пересланное сообщение
        public List<EmailMessageBlock> Blocks { get; set; } = new();
    }
}
