using System;
using System.Collections.Generic;

namespace TelegramEmailBot.Models
{
    public class ForwardedMessage
    {
        public int MessageId { get; set; }
        public string SenderInfo { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public List<AttachmentData> Attachments { get; set; } = new();
        public long? SenderId { get; set; }
    }
}
