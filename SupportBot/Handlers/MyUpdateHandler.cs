using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Logging;
using TelegramEmailBot.Models;
using TelegramEmailBot.Models.Interfaces;
using TelegramEmailBot.Services;
using Telegram.Bot.Polling;

namespace TelegramEmailBot.Handlers
{
    public class MyUpdateHandler : IUpdateHandler
    {
        private readonly TelegramMessageProcessor _messageProcessor;
        private readonly EmailGroupingManager _groupingManager;
        private readonly CompanyBindingService _companyBindingService;
        private readonly CompanyListService _companyListService;
        private readonly List<string> _companySuggestions;
        private readonly ILogger<MyUpdateHandler> _logger;
        // Словарь для ожидающих привязок: chatId -> senderId
        private readonly Dictionary<long, long> _pendingBindings = new Dictionary<long, long>();

        public MyUpdateHandler(
            TelegramMessageProcessor messageProcessor,
            EmailGroupingManager groupingManager,
            CompanyBindingService companyBindingService,
            CompanyListService companyListService,
            ILogger<MyUpdateHandler> logger)
        {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _groupingManager = groupingManager ?? throw new ArgumentNullException(nameof(groupingManager));
            _companyBindingService = companyBindingService ?? throw new ArgumentNullException(nameof(companyBindingService));
            _companyListService = companyListService ?? throw new ArgumentNullException(nameof(companyListService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _companySuggestions = _companyListService.GetCompanies();
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update == null) return;

            if (update.Type == UpdateType.CallbackQuery)
            {
                await ProcessCallbackQueryAsync(botClient, update.CallbackQuery!, cancellationToken);
                return;
            }

            if (update.Type != UpdateType.Message)
                return;

            var message = update.Message;
            if (message == null)
                return;

            string text = message.Text?.Trim() ?? string.Empty;

            if (text.StartsWith("/bind", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Обработка команды /bind в чате {ChatId}", message.Chat.Id);
                await ProcessBindCommandAsync(botClient, message, cancellationToken);
                return;
            }
            if (text.Equals("/no", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Обработка команды /no в чате {ChatId}", message.Chat.Id);
                await ProcessNoCommandAsync(botClient, message, cancellationToken);
                return;
            }

            if (message.ForwardFrom == null && string.IsNullOrEmpty(message.ForwardSenderName))
            {
                _logger.LogWarning("Сообщение в чате {ChatId} не является пересланным.", message.Chat.Id);
                await botClient.SendMessage(message.Chat.Id,
                    "Пожалуйста, пришлите пересланное сообщение.", cancellationToken: cancellationToken);
                return;
            }

            // Используем адаптер для преобразования Message в IIncomingMessage
            ForwardedMessage forwardedMessage = await _messageProcessor.ProcessMessageAsync(botClient, new TelegramMessageAdapter(message), cancellationToken);
            _logger.LogInformation("Обработано пересланное сообщение из чата {ChatId}", message.Chat.Id);

            if (forwardedMessage.SenderId.HasValue)
            {
                long senderId = forwardedMessage.SenderId.Value;
                if (!_companyBindingService.TryGetCompany(senderId, out string? company))
                {
                    bool pendingExists;
                    lock (_pendingBindings)
                    {
                        pendingExists = _pendingBindings.ContainsKey(message.Chat.Id);
                        if (!pendingExists)
                        {
                            _pendingBindings[message.Chat.Id] = senderId;
                        }
                    }
                    if (!pendingExists)
                    {
                        var buttons = new List<InlineKeyboardButton>();
                        foreach (var suggestion in _companySuggestions)
                        {
                            buttons.Add(InlineKeyboardButton.WithCallbackData(suggestion, $"bind:{suggestion}"));
                        }
                        buttons.Add(InlineKeyboardButton.WithCallbackData("Нет", "no"));
                        var keyboard = new InlineKeyboardMarkup(buttons);
                        await botClient.SendMessage(message.Chat.Id,
                            "Отправитель не привязан к компании. Выберите из предложенных вариантов или введите команду /bind <название компании> для ручной привязки. Если не хотите привязывать, нажмите «Нет» или введите /no.",
                            replyMarkup: keyboard, cancellationToken: cancellationToken);
                    }
                }
            }

            _groupingManager.AddMessage(message.Chat.Id, forwardedMessage, botClient, cancellationToken);
            await botClient.SendMessage(message.Chat.Id, "Сообщение добавлено в группу.", cancellationToken: cancellationToken);
        }

        private async Task ProcessCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            if (callbackQuery == null) return;

            _logger.LogInformation("Получен CallbackQuery от пользователя {UserId}", callbackQuery.From.Id);
            string data = callbackQuery.Data ?? string.Empty;
            if (data.StartsWith("bind:", StringComparison.OrdinalIgnoreCase))
            {
                string company = data.Substring("bind:".Length).Trim();
                long chatId = callbackQuery.Message?.Chat.Id ?? 0;
                if (chatId == 0) return;

                long senderId;
                bool found;
                lock (_pendingBindings)
                {
                    found = _pendingBindings.TryGetValue(chatId, out senderId);
                }
                if (!found)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Нет ожидающей привязки.", cancellationToken: cancellationToken);
                    return;
                }
                _companyBindingService.BindCompany(senderId, company);
                _companyListService.AddCompany(company);
                if (!_companySuggestions.Contains(company, StringComparer.OrdinalIgnoreCase))
                {
                    _companySuggestions.Add(company);
                }
                lock (_pendingBindings)
                {
                    _pendingBindings.Remove(chatId);
                }
                await botClient.AnswerCallbackQuery(callbackQuery.Id, $"Привязано: {company}", cancellationToken: cancellationToken);
                await botClient.SendMessage(chatId, $"Привязано: {company}", cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(chatId, botClient, cancellationToken);
            }
            else if (data.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                long chatId = callbackQuery.Message?.Chat.Id ?? 0;
                lock (_pendingBindings)
                {
                    _pendingBindings.Remove(chatId);
                }
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Привязка отменена", cancellationToken: cancellationToken);
                await botClient.SendMessage(chatId, "Привязка отменена. Сообщения будут отправляться с обычной темой.", cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(chatId, botClient, cancellationToken);
            }
        }

        private async Task ProcessBindCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            string[] parts = message.Text?.Split(' ', 2) ?? Array.Empty<string>();
            string input = parts.Length >= 2 ? parts[1].Trim() : string.Empty;

            long senderId = 0;
            lock (_pendingBindings)
            {
                if (!_pendingBindings.TryGetValue(message.Chat.Id, out senderId) && message.ForwardFrom != null)
                {
                    senderId = message.ForwardFrom.Id;
                }
            }
            if (senderId == 0)
            {
                await botClient.SendMessage(message.Chat.Id, "Нет информации для привязки. Сначала перешлите сообщение.", cancellationToken: cancellationToken);
                return;
            }

            var filtered = string.IsNullOrEmpty(input)
                           ? _companySuggestions
                           : _companySuggestions.FindAll(c => c.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);

            if (filtered.Count == 0)
            {
                _companyBindingService.BindCompany(senderId, input);
                _companyListService.AddCompany(input);
                if (!_companySuggestions.Contains(input, StringComparer.OrdinalIgnoreCase))
                    _companySuggestions.Add(input);
                lock (_pendingBindings)
                {
                    _pendingBindings.Remove(message.Chat.Id);
                }
                await botClient.SendMessage(message.Chat.Id, $"Привязано: {input}", cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(message.Chat.Id, botClient, cancellationToken);
                return;
            }
            if (filtered.Count == 1)
            {
                _companyBindingService.BindCompany(senderId, filtered[0]);
                _companyListService.AddCompany(filtered[0]);
                if (!_companySuggestions.Contains(filtered[0], StringComparer.OrdinalIgnoreCase))
                    _companySuggestions.Add(filtered[0]);
                lock (_pendingBindings)
                {
                    _pendingBindings.Remove(message.Chat.Id);
                }
                await botClient.SendMessage(message.Chat.Id, $"Привязано: {filtered[0]}", cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(message.Chat.Id, botClient, cancellationToken);
                return;
            }
            var buttons = filtered.ConvertAll(c => InlineKeyboardButton.WithCallbackData(c, $"bind:{c}"));
            buttons.Add(InlineKeyboardButton.WithCallbackData("Нет", "no"));
            var keyboard = new InlineKeyboardMarkup(buttons);
            await botClient.SendMessage(message.Chat.Id, "Выберите компанию из предложенных вариантов:", replyMarkup: keyboard, cancellationToken: cancellationToken);
        }

        private async Task ProcessNoCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            lock (_pendingBindings)
            {
                _pendingBindings.Remove(message.Chat.Id);
            }
            await botClient.SendMessage(message.Chat.Id, "Привязка отменена. Будут использованы стандартные параметры при формировании темы письма.", cancellationToken: cancellationToken);
            await _groupingManager.TryTriggerPendingEmailAsync(message.Chat.Id, botClient, cancellationToken);
        }

        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource errorSource, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Ошибка обновления. Источник: {ErrorSource}", errorSource);
            return Task.CompletedTask;
        }
    }
}
