using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramEmailBot.Models;
using TelegramEmailBot.Services;

namespace TelegramEmailBot.Handlers
{
    public class MyUpdateHandler : IUpdateHandler
    {
        private readonly TelegramMessageProcessor _messageProcessor;
        private readonly EmailGroupingManager _groupingManager;
        private readonly CompanyBindingService _companyBindingService;
        private readonly CompanyListService _companyListService;
        private readonly List<string> _companySuggestions;  // Загружаем из CSV

        // Для каждого чата сохраняем ожидающую привязку: chatId -> senderId
        private readonly Dictionary<long, long> _pendingBindings = new Dictionary<long, long>();

        public MyUpdateHandler(TelegramMessageProcessor messageProcessor,
                               EmailGroupingManager groupingManager,
                               CompanyBindingService companyBindingService,
                               CompanyListService companyListService)
        {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _groupingManager = groupingManager ?? throw new ArgumentNullException(nameof(groupingManager));
            _companyBindingService = companyBindingService ?? throw new ArgumentNullException(nameof(companyBindingService));
            _companyListService = companyListService ?? throw new ArgumentNullException(nameof(companyBindingService));
            _companySuggestions = _companyListService.GetCompanies();
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Обрабатываем CallbackQuery (кнопки inline)
            if (update.Type == UpdateType.CallbackQuery)
            {
                await ProcessCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
                return;
            }

            if (update.Type != UpdateType.Message)
                return;

            var message = update.Message;
            if (message == null)
                return;

            string text = message.Text?.Trim() ?? string.Empty;
            // Если сообщение начинается с /bind – обрабатываем команду bind
            if (text.StartsWith("/bind", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessBindCommandAsync(botClient, message, cancellationToken);
                return;
            }
            if (text.Equals("/no", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessNoCommandAsync(botClient, message, cancellationToken);
                return;
            }

            // Если сообщение не пересланное – просим его переслать
            if (message.ForwardFrom == null && string.IsNullOrEmpty(message.ForwardSenderName))
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Пожалуйста, пришлите пересланное сообщение.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Обрабатываем пересланное сообщение
            ForwardedMessage forwardedMessage = await _messageProcessor.ProcessMessageAsync(botClient, message, cancellationToken);

            // Если имеется информация об отправителе (SenderId) – проверяем привязку
            if (forwardedMessage.SenderId.HasValue)
            {
                long senderId = forwardedMessage.SenderId.Value;
                if (!_companyBindingService.TryGetCompany(senderId, out string company))
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
                        // Формируем inline‑клавиатуру на основе подсказок
                        var buttons = new List<InlineKeyboardButton>();
                        foreach (var suggestion in _companySuggestions)
                        {
                            buttons.Add(InlineKeyboardButton.WithCallbackData(suggestion, $"bind:{suggestion}"));
                        }
                        buttons.Add(InlineKeyboardButton.WithCallbackData("Нет", "no"));
                        var keyboard = new InlineKeyboardMarkup(buttons);
                        await botClient.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: "Отправитель не привязан к компании. Выберите из предложенных вариантов или введите команду /bind <название компании> для ручной привязки. Если не хотите привязывать, нажмите «Нет» или введите /no.",
                            replyMarkup: keyboard,
                            cancellationToken: cancellationToken);
                    }
                }
            }

            // Передаем сообщение для группировки (отправка email не прерывается)
            _groupingManager.AddMessage(message.Chat.Id, forwardedMessage, botClient, cancellationToken);
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Сообщение добавлено в группу.",
                cancellationToken: cancellationToken);
        }

        private async Task ProcessCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            string data = callbackQuery.Data; // Форматы: "bind:Название компании" или "no"
            if (data.StartsWith("bind:", StringComparison.OrdinalIgnoreCase))
            {
                string company = data.Substring("bind:".Length).Trim();
                long chatId = callbackQuery.Message.Chat.Id;
                long senderId;
                bool found;
                lock (_pendingBindings)
                {
                    found = _pendingBindings.TryGetValue(chatId, out senderId);
                }
                if (!found)
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Нет ожидающей привязки.", cancellationToken: cancellationToken);
                    return;
                }
                _companyBindingService.BindCompany(senderId, company);
                // Если компании ещё нет в списке, добавляем её
                _companyListService.AddCompany(company);
                // Также обновляем локальный список подсказок
                if (!_companySuggestions.Contains(company, StringComparer.OrdinalIgnoreCase))
                {
                    _companySuggestions.Add(company);
                }
                lock (_pendingBindings)
                {
                    _pendingBindings.Remove(chatId);
                }
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"Привязано: {company}", cancellationToken: cancellationToken);
                await botClient.SendTextMessageAsync(chatId, $"Привязано: {company}", cancellationToken: cancellationToken);
                // Если для этого чата группа писем ожидала привязку, пытаемся отправить письмо
                await _groupingManager.TryTriggerPendingEmailAsync(chatId, botClient, cancellationToken);
            }
            else if (data.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                long chatId = callbackQuery.Message.Chat.Id;
                lock (_pendingBindings)
                {
                    _pendingBindings.Remove(chatId);
                }
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Привязка отменена", cancellationToken: cancellationToken);
                await botClient.SendTextMessageAsync(chatId, "Привязка отменена. Сообщения будут отправляться с обычной темой.", cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(chatId, botClient, cancellationToken);
            }
        }

        private async Task ProcessBindCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            // Формат команды: /bind или /bind <название компании>
            string[] parts = message.Text.Split(' ', 2);
            string input = parts.Length >= 2 ? parts[1].Trim() : string.Empty;

            // Получаем senderId – если он не в pending, пытаемся взять из ForwardFrom
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
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Нет информации для привязки. Сначала перешлите сообщение.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Если введена подстрока, фильтруем список компаний
            var filtered = string.IsNullOrEmpty(input)
                           ? _companySuggestions
                           : _companySuggestions.Where(c => c.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            if (filtered.Count == 0)
            {
                // Если в списке нет совпадений – добавляем компанию сразу
                _companyBindingService.BindCompany(senderId, input);
                _companyListService.AddCompany(input);
                if (!_companySuggestions.Contains(input, StringComparer.OrdinalIgnoreCase))
                    _companySuggestions.Add(input);
                lock (_pendingBindings)
                {
                    _pendingBindings.Remove(message.Chat.Id);
                }
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Привязано: {input}",
                    cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(message.Chat.Id, botClient, cancellationToken);
                return;
            }
            if (filtered.Count == 1)
            {
                _companyBindingService.BindCompany(senderId, filtered.First());
                _companyListService.AddCompany(filtered.First());
                if (!_companySuggestions.Contains(filtered.First(), StringComparer.OrdinalIgnoreCase))
                    _companySuggestions.Add(filtered.First());
                lock (_pendingBindings)
                {
                    _pendingBindings.Remove(message.Chat.Id);
                }
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Привязано: {filtered.First()}",
                    cancellationToken: cancellationToken);
                await _groupingManager.TryTriggerPendingEmailAsync(message.Chat.Id, botClient, cancellationToken);
                return;
            }
            // Если найдено несколько вариантов – показываем inline‑клавиатуру для выбора
            var buttons = filtered.Select(c => InlineKeyboardButton.WithCallbackData(c, $"bind:{c}")).ToList();
            buttons.Add(InlineKeyboardButton.WithCallbackData("Нет", "no"));
            var keyboard = new InlineKeyboardMarkup(buttons);
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Выберите компанию из предложенных вариантов:",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        private async Task ProcessNoCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            lock (_pendingBindings)
            {
                _pendingBindings.Remove(message.Chat.Id);
            }
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Привязка отменена. Будут использованы стандартные параметры при формировании темы письма.",
                cancellationToken: cancellationToken);
            await _groupingManager.TryTriggerPendingEmailAsync(message.Chat.Id, botClient, cancellationToken);
        }

        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource errorSource, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка обновления: {exception.Message}");
            Console.WriteLine($"Источник ошибки: {errorSource}");
            return Task.CompletedTask;
        }
    }
}
