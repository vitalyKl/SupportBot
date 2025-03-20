using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramEmailBot.Handlers.Commands
{
    public interface ICommandHandler
    {
        Task ProcessMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken);
        Task ProcessCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken);
    }
}
