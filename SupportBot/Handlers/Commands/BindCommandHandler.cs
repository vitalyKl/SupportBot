using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;
using TelegramEmailBot.Services;

namespace TelegramEmailBot.Handlers.Commands
{
    public class BindCommandHandler : BaseCommandHandler
    {
        private readonly ILogger<BindCommandHandler> _logger;
        private readonly CompanyBindingService _companyBindingService;
        private readonly CompanyListService _companyListService;

        public BindCommandHandler(ILogger<BindCommandHandler> logger, CompanyBindingService companyBindingService, CompanyListService companyListService)
        {
            _logger = logger;
            _companyBindingService = companyBindingService;
            _companyListService = companyListService;
        }

        public override async Task ProcessMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            // Если команда /bind имеет аргумент, используем его для привязки.
            string[] parts = message.Text?.Split(' ', 2) ?? new string[0];
            if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                string companyName = parts[1].Trim();
                _companyBindingService.BindCompany(message.From.Id, companyName);
                _companyListService.AddCompany(companyName);
                await botClient.SendMessage(
                    message.Chat.Id,
                    $"Bound: {companyName}",
                    parseMode: ParseMode.None,
                    disableNotification: false,
                    replyMarkup: null,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "Usage: /bind <CompanyName> to bind your organization.",
                    parseMode: ParseMode.None,
                    disableNotification: false,
                    replyMarkup: null,
                    cancellationToken: cancellationToken);
            }
        }

        public override async Task ProcessCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                text: "Bind callback received. (Not fully implemented)",
                showAlert: false,
                url: null,
                cacheTime: null,
                cancellationToken: cancellationToken);
        }
    }
}
