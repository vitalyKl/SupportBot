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
using Microsoft.Extensions.Options;
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
        private readonly ILogger<MyUpdateHandler> _logger;
        private readonly List<string> _companySuggestions;
        private readonly long _authorizedUserId;
        // Для каждого чата сохраняем ожидающую привязку: chatId -> senderId
        private readonly Dictionary<long, long> _pendingBindings = new();

        public MyUpdateHandler(
            TelegramMessageProcessor messageProcessor,
            EmailGroupingManager groupingManager,
            CompanyBindingService companyBindingService,
            CompanyListService companyListService,
            ILogger<MyUpdateHandler> logger,
            IOptions<AccessOptions> accessOptions)
        {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _groupingManager = groupingManager ?? throw new ArgumentNullException(nameof(groupingManager));
            _companyBindingService = companyBindingService ?? throw new ArgumentNullException(nameof(companyBindingService));
            _companyListService = companyListService ?? throw new ArgumentNullException(nameof(companyListService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _companySuggestions = _companyListService.GetCompanies();
            _authorizedUserId = accessOptions.Value.AuthorizedUserId;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Ограничиваем доступ только для авторизованного пользователя
            if (update.Message != null && update.Message.From != null &&
                update.Message.From.Id != _authorizedUserId)
            {
                _logger.LogWarning("Access denied for user {UserId}.", update.Message.From.Id);
                await botClient.SendMessage(update.Message.Chat.Id, "Access denied.", cancellationToken: cancellationToken);
                return;
            }

            // Обрабатываем callback (inline клавиатура)
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
                _logger.LogInformation("Processing /bind command in chat {ChatId}.", message.Chat.Id);
                await ProcessBindCommandAsync(botClient, message, cancellationToken);
                return;
            }
            if (text.StartsWith("/unbind", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Processing /unbind command in chat {ChatId}.", message.Chat.Id);
                await ProcessUnbindCommandAsync(botClient, message, cancellationToken);
                return;
            }
            if (text.Equals("/no", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Processing /no command in chat {ChatId}.", message.Chat.Id);
                await ProcessNoCommandAsync(botClient, message, cancellationToken);
                return;
            }

            if (message.ForwardFrom == null && string.IsNullOrEmpty(message.ForwardSenderName))
            {
                _logger.LogWarning("Message in chat {ChatId} is not forwarded.", message.Chat.Id);
                await botClient.SendMessage(message.Chat.Id, "Please forward a message.", cancellationToken: cancellationToken);
                return;
            }

            // Обработка пересланного сообщения через адаптер
            ForwardedMessage forwardedMessage = await _messageProcessor.ProcessMessageAsync(botClient, new TelegramMessageAdapter(message), cancellationToken);
            _logger.LogInformation("Processed forwarded message in chat {ChatId}.", message.Chat.Id);

            // Если сообщение не сгенерировано через OCR/ML (обычное пересланное сообщение) — проверяем привязку
            if (forwardedMessage.SenderId.HasValue)
            {
                long senderId = forwardedMessage.SenderId.Value;
                if (!_companyBindingService.TryGetCompany(senderId, out _))
                {
                    // Если привязки не найдено, вызываем метод для показа клавиатуры с буквами
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
                        var keyboard = GetLetterKeyboard(message.Chat.Id);
                        await botClient.SendMessage(message.Chat.Id,
                            "Sender not bound to an organization. Please choose a letter to filter organizations or use /bind <organization> for manual binding. To unbind, use /unbind.",
                            replyMarkup: keyboard,
                            cancellationToken: cancellationToken);
                    }
                }
            }

            _groupingManager.AddMessage(message.Chat.Id, forwardedMessage, botClient, cancellationToken);
            await botClient.SendMessage(message.Chat.Id, "Message added to group.", cancellationToken: cancellationToken);
        }

        // Формирует клавиатуру с кнопками для выбора первых букв организаций
        private InlineKeyboardMarkup GetLetterKeyboard(long chatId)
        {
            // Извлекаем уникальные первые буквы всех компаний, приводим к верхнему регистру и сортируем
            var letters = _companySuggestions
                .Select(c => c.Trim().Substring(0, 1).ToUpper())
                .Distinct()
                .OrderBy(l => l)
                .ToList();

            var rows = new List<InlineKeyboardButton[]>();
            // Каждая кнопка в отдельном ряду (столбиком)
            foreach (var letter in letters)
            {
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData(letter, $"letter:{letter}") });
            }
            // Можно добавить кнопку "Все"
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Все", "letter:ALL") });
            return new InlineKeyboardMarkup(rows);
        }


        // Формирует клавиатуру с перечнем компаний, начинающихся на указанную букву.
        private InlineKeyboardMarkup GetCompaniesKeyboardForLetter(string letter)
        {
            IEnumerable<string> filtered;
            if (letter.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                filtered = _companySuggestions;
            }
            else
            {
                filtered = _companySuggestions.Where(c => c.StartsWith(letter, StringComparison.OrdinalIgnoreCase));
            }

            var rows = filtered.Select(c => new[] { InlineKeyboardButton.WithCallbackData(c, $"bind:{c}") }).ToList();
            // Добавляем кнопку "Назад" для возврата к выбору букв
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "letters") });
            return new InlineKeyboardMarkup(rows);
        }


        private async Task ProcessCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            if (callbackQuery == null)
                return;

            _logger.LogInformation("Received CallbackQuery from user {UserId}.", callbackQuery.From.Id);
            string data = callbackQuery.Data ?? string.Empty;

            // Если данные начинаются с "letter:" – обновляем клавиатуру для выбора компаний по букве.
            if (data.StartsWith("letter:", StringComparison.OrdinalIgnoreCase))
            {
                string letter = data.Substring("letter:".Length).Trim();
                long chatId = callbackQuery.Message?.Chat.Id ?? 0;
                var keyboard = GetCompaniesKeyboardForLetter(letter);
                // Обновляем сообщение с клавиатурой
                await botClient.EditMessageReplyMarkupAsync(chatId, callbackQuery.Message!.MessageId, replyMarkup: keyboard, cancellationToken: cancellationToken);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }

            // Если данные равны "letters", то возвращаем начальный список букв для фильтрации.
            if (data.Equals("letters", StringComparison.OrdinalIgnoreCase))
            {
                long chatId = callbackQuery.Message?.Chat.Id ?? 0;
                var keyboard = GetLetterKeyboard(chatId);
                await botClient.EditMessageReplyMarkupAsync(chatId, callbackQuery.Message!.MessageId, replyMarkup: keyboard, cancellationToken: cancellationToken);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }

            // Если данные начинаются с "bind:" – обрабатываем выбор компании
            if (data.StartsWith("bind:", StringComparison.OrdinalIgnoreCase))
            {
                string company = data.Substring("bind:".Length).Trim();
                long chatId = callbackQuery.Message?.Chat.Id ?? 0;
                if (chatId == 0)
                    return;
                long senderId;
                bool found;
                lock (_pendingBindings)
                {
                    found = _pendingBindings.TryGetValue(chatId, out senderId);
                }
                if (!found)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "No pending binding.", cancellationToken: cancellationToken);
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
                await botClient.AnswerCallbackQuery(callbackQuery.Id, $"Bound: {company}", cancellationToken: cancellationToken);
                await botClient.SendMessage(chatId, $"Bound: {company}", cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(chatId, botClient, cancellationToken);
                return;
            }
            else if (data.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                long chatId = callbackQuery.Message?.Chat.Id ?? 0;
                lock (_pendingBindings)
                {
                    _pendingBindings.Remove(chatId);
                }
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Binding canceled", cancellationToken: cancellationToken);
                await botClient.SendMessage(chatId, "Binding canceled. Default subject will be used.", cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(chatId, botClient, cancellationToken);
            }
        }


        private async Task ProcessBindCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            // Здесь используется динамическая фильтрация по введенной подстроке
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
                await botClient.SendMessage(message.Chat.Id, "No binding information. Please forward a message first.", cancellationToken: cancellationToken);
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
                await botClient.SendMessage(message.Chat.Id, $"Bound: {input}", cancellationToken: cancellationToken);
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
                await botClient.SendMessage(message.Chat.Id, $"Bound: {filtered[0]}", cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(message.Chat.Id, botClient, cancellationToken);
                return;
            }
            // Если найдено несколько вариантов – предлагаем пагинацию через буквы.
            var keyboard = GetLetterKeyboard(message.Chat.Id);
            await botClient.SendMessage(message.Chat.Id, "Choose a starting letter to filter companies:", replyMarkup: keyboard, cancellationToken: cancellationToken);
        }

        private async Task ProcessUnbindCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            if (message == null)
                return;
            long senderId = message.From?.Id ?? 0;
            if (senderId == 0)
            {
                await botClient.SendMessage(message.Chat.Id, "Unable to determine your user ID for unbinding.", cancellationToken: cancellationToken);
                return;
            }
            // Для отвязки устанавливаем пустую строку как привязку.
            _companyBindingService.BindCompany(senderId, string.Empty);
            await botClient.SendMessage(message.Chat.Id, "You have been unbound from any organization.", cancellationToken: cancellationToken);
        }

        private async Task ProcessNoCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            lock (_pendingBindings)
            {
                _pendingBindings.Remove(message.Chat.Id);
            }
            await botClient.SendMessage(message.Chat.Id, "Binding canceled. Default subject will be used.", cancellationToken: cancellationToken);
            await _groupingManager.TryTriggerPendingEmailAsync(message.Chat.Id, botClient, cancellationToken);
        }

        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource errorSource, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Update error. Source: {ErrorSource}", errorSource);
            return Task.CompletedTask;
        }
    }
}
