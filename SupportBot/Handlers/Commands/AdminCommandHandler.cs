using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramEmailBot.Models;
using TelegramEmailBot.Handlers.Keyboards;

namespace TelegramEmailBot.Handlers.Commands
{
    public class AdminCommandHandler : BaseCommandHandler
    {
        private readonly ILogger<AdminCommandHandler> _logger;
        private readonly long _authorizedUserId;
        private readonly KeyboardGenerator _keyboardGenerator;

        public AdminCommandHandler(ILogger<AdminCommandHandler> logger, IOptions<AccessOptions> accessOptions, KeyboardGenerator keyboardGenerator)
        {
            _logger = logger;
            _authorizedUserId = accessOptions.Value.AuthorizedUserId;
            _keyboardGenerator = keyboardGenerator;
        }

        public override async Task ProcessMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            // Вывод главного меню админа
            var keyboard = _keyboardGenerator.GenerateAdminPanelKeyboard();
            await botClient.SendMessage(
                message.Chat.Id,
                "Admin panel: choose an option.",
                parseMode: ParseMode.None,
                disableNotification: false,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        public override async Task ProcessCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await _keyboardGenerator.ProcessAdminCallbackAsync(botClient, callbackQuery, cancellationToken);
        }
    }
}
