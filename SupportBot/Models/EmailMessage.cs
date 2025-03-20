using System;
using System.Collections.Generic;
using System.Net.Mail;

namespace TelegramEmailBot.Models
{
    public class EmailMessage : MailMessage
    {
        // Дополнительные поля для конвейера обработки.
        public long SenderId { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public string SenderFullName { get; set; } = string.Empty;
        public DateTime SentDate { get; set; }
        // Мы можем добавить список вложений, порядок которых важен
        public List<Attachment> OrderedAttachments { get; } = new List<Attachment>();

        // Дополнительное свойство для хранения исходного текста (возможно, объединенного из нескольких сообщений)
        public string CombinedBody { get; set; } = string.Empty;

        // Можно добавить дополнительные поля для отслеживания состояния конвейера
    }
}
