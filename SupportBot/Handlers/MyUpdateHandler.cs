using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramEmailBot.Models;
using TelegramEmailBot.Handlers;
using Telegram.Bot.Polling;  // Для HandleErrorSource

namespace TelegramEmailBot.Handlers
{
    public class MyUpdateHandler : IUpdateHandler
    {
        private readonly ILogger<MyUpdateHandler> _logger;
        private readonly IOptions<AccessOptions> _accessOptions;
        private readonly CommandDispatcher _dispatcher;

        public MyUpdateHandler(
            ILogger<MyUpdateHandler> logger,
            IOptions<AccessOptions> accessOptions,
            CommandDispatcher dispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accessOptions = accessOptions ?? throw new ArgumentNullException(nameof(accessOptions));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Если update является сообщением, то проводим базовую авторизацию
            if (update.Message != null)
            {
                if (update.Message.From == null || update.Message.From.Id != _accessOptions.Value.AuthorizedUserId)
                {
                    _logger.LogWarning("Access denied for user {UserId}.", update.Message.From?.Id);
                    await botClient.SendMessage(
                        update.Message.Chat.Id,
                        "Access denied.",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.None,
                        disableNotification: false,
                        replyMarkup: null,
                        cancellationToken: cancellationToken);
                    return;
                }
            }

            // Независимо от типа update (сообщение или callback), передаем его в CommandDispatcher,
            // который уже сам решит, какая логика (команда или конвейер) должна быть применена.
            await _dispatcher.DispatchAsync(botClient, update, cancellationToken);
        }

        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource errorSource, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Update error. Source: {ErrorSource}", errorSource);
            return Task.CompletedTask;
        }
    }
}
