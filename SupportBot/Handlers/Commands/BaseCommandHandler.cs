using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramEmailBot.Handlers.Commands
{
    public abstract class BaseCommandHandler : ICommandHandler
    {
        public abstract Task ProcessMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken);
        public abstract Task ProcessCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken);
    }
}
