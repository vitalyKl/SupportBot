using System;
using System.Collections.Generic;

namespace TelegramEmailBot.Models
{
    public class ForwardedMessage
    {
        public int MessageId { get; set; }
        public string SenderInfo { get; set; }
        public string Text { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public List<AttachmentData> Attachments { get; set; } = new List<AttachmentData>();

        // Новый параметр – Telegram ID отправителя (если доступен)
        public long? SenderId { get; set; }
    }
}
