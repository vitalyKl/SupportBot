using System;
using System.Collections.Generic;

namespace SupportBot.Core.Entities
{
    public class EmailMessage
    {
        // Основная информация об отправителе
        public string SenderFirstName { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderUserName { get; set; } = string.Empty;
        public DateTime FirstMessageDate { get; set; }

        // Если автор привязан к компании – здесь название компании
        public string BoundCompany { get; set; } = string.Empty;

        // Накопленные тексты пересланных сообщений
        public List<string> MessageTexts { get; set; } = new();
    }
}
