using Telegram.Bot.Types;

namespace TelegramEmailBot.Services
{
    public class TelegramMessageAdapter
    {
        public Message Message { get; }

        public TelegramMessageAdapter(Message message)
        {
            Message = message;
        }

    }
}
