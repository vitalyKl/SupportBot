using System;
using Telegram.Bot.Types;
using TelegramEmailBot.Models.Interfaces;

namespace SupportBot.Tests
{
    public class FakeIncomingMessage : IIncomingMessage
    {
        public int MessageId { get; set; }
        public DateTime Date { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public Contact? Contact { get; set; }
        public PhotoSize[] Photo { get; set; } = Array.Empty<PhotoSize>();
        public Document? Document { get; set; }
        public User? ForwardFrom { get; set; }
        public string ForwardSenderName { get; set; } = string.Empty;
    }
}
