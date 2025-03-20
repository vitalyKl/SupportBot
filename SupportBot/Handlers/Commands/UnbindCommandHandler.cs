using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;
using TelegramEmailBot.Services;

namespace TelegramEmailBot.Handlers.Commands
{
    public class UnbindCommandHandler : BaseCommandHandler
    {
        private readonly ILogger<UnbindCommandHandler> _logger;
        private readonly CompanyBindingService _companyBindingService;

        public UnbindCommandHandler(ILogger<UnbindCommandHandler> logger, CompanyBindingService companyBindingService)
        {
            _logger = logger;
            _companyBindingService = companyBindingService;
        }

        public override async Task ProcessMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            _companyBindingService.BindCompany(message.From.Id, string.Empty);
            await botClient.SendMessage(
                message.Chat.Id,
                "You have been unbound from any organization.",
                parseMode: ParseMode.None,
                disableNotification: false,
                replyMarkup: null,
                cancellationToken: cancellationToken);
        }

        public override async Task ProcessCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                text: "Unbind callback received. (Not fully implemented)",
                showAlert: false,
                url: null,
                cacheTime: null,
                cancellationToken: cancellationToken);
        }
    }
}
