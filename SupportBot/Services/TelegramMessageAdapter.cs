using Telegram.Bot.Types;
using TelegramEmailBot.Models.Interfaces;

namespace TelegramEmailBot.Services
{
    public class TelegramMessageAdapter : IIncomingMessage
    {
        private readonly Message _message;

        public TelegramMessageAdapter(Message message)
        {
            _message = message;
        }

        public int MessageId => _message.MessageId;
        public DateTime Date => _message.Date;
        public string Text => _message.Text ?? string.Empty;
        public string Caption => _message.Caption ?? string.Empty;
        public Contact? Contact => _message.Contact;
        public PhotoSize[] Photo => _message.Photo ?? System.Array.Empty<PhotoSize>();
        public Document? Document => _message.Document;
        public User? ForwardFrom => _message.ForwardFrom;
        public string ForwardSenderName => _message.ForwardSenderName ?? string.Empty;
    }
}
