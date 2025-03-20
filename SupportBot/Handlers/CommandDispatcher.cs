using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Logging;
using TelegramEmailBot.Models;
using TelegramEmailBot.Services;
using TelegramEmailBot.Handlers.Keyboards;
using TelegramEmailBot.Handlers.Commands;

namespace TelegramEmailBot.Handlers
{
    public class CommandDispatcher
    {
        private readonly ILogger<CommandDispatcher> _logger;
        private readonly AdminCommandHandler _adminCommandHandler;
        private readonly BindCommandHandler _bindCommandHandler;
        private readonly UnbindCommandHandler _unbindCommandHandler;
        private readonly EmailPipelineProcessor _pipelineProcessor;
        private readonly EmailSender _emailSender;
        private readonly CompanyBindingService _companyBindingService;
        private readonly CompanyListService _companyListService;
        private readonly KeyboardGenerator _keyboardGenerator;
        private readonly BindingEnricher _bindingEnricher;
        private readonly ProgressKeyboardService _progressKeyboardService;

        // Для работы с одним чатом используется один pending email
        private EmailMessage _pendingEmail = null;
        private bool _progressDisplayed = false;

        public CommandDispatcher(
            ILogger<CommandDispatcher> logger,
            AdminCommandHandler adminCommandHandler,
            BindCommandHandler bindCommandHandler,
            UnbindCommandHandler unbindCommandHandler,
            EmailPipelineProcessor pipelineProcessor,
            EmailSender emailSender,
            CompanyBindingService companyBindingService,
            CompanyListService companyListService,
            KeyboardGenerator keyboardGenerator,
            BindingEnricher bindingEnricher,
            ProgressKeyboardService progressKeyboardService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _adminCommandHandler = adminCommandHandler ?? throw new ArgumentNullException(nameof(adminCommandHandler));
            _bindCommandHandler = bindCommandHandler ?? throw new ArgumentNullException(nameof(bindCommandHandler));
            _unbindCommandHandler = unbindCommandHandler ?? throw new ArgumentNullException(nameof(unbindCommandHandler));
            _pipelineProcessor = pipelineProcessor ?? throw new ArgumentNullException(nameof(pipelineProcessor));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            _companyBindingService = companyBindingService ?? throw new ArgumentNullException(nameof(companyBindingService));
            _companyListService = companyListService ?? throw new ArgumentNullException(nameof(companyListService));
            _keyboardGenerator = keyboardGenerator ?? throw new ArgumentNullException(nameof(keyboardGenerator));
            _bindingEnricher = bindingEnricher ?? throw new ArgumentNullException(nameof(bindingEnricher));
            _progressKeyboardService = progressKeyboardService ?? throw new ArgumentNullException(nameof(progressKeyboardService));
        }

        public async Task DispatchAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message != null)
            {
                string text = update.Message.Text?.Trim() ?? string.Empty;
                if (text.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
                {
                    await _adminCommandHandler.ProcessMessageAsync(botClient, update.Message, cancellationToken);
                }
                else if (text.StartsWith("/bind", StringComparison.OrdinalIgnoreCase))
                {
                    await _bindCommandHandler.ProcessMessageAsync(botClient, update.Message, cancellationToken);
                    if (_pendingEmail != null)
                    {
                        _bindingEnricher.EnrichEmailBinding(_pendingEmail);
                        await _emailSender.SendEmailAsync(_pendingEmail, cancellationToken);
                        _pendingEmail = null;
                        _progressDisplayed = false;
                        await botClient.SendMessage(
                            update.CallbackQuery.Message.Chat.Id,
                            "Email sent successfully.",
                            parseMode: ParseMode.None,
                            disableNotification: false,
                            replyMarkup: null,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.AnswerCallbackQuery(
                            update.CallbackQuery.Id,
                            text: "No pending email found.",
                            showAlert: false,
                            url: null,
                            cacheTime: null,
                            cancellationToken: cancellationToken);
                    }
                }
                else if (text.StartsWith("/unbind", StringComparison.OrdinalIgnoreCase))
                {
                    await _unbindCommandHandler.ProcessMessageAsync(botClient, update.Message, cancellationToken);
                }
                else
                {
                    // Обработка пересылаемого сообщения (не являющегося командой).
                    if (_pendingEmail == null)
                    {
                        // Создаем новый pending email с базовыми данными об отправителе.
                        _pendingEmail = CreateEmailMessageFromUpdate(update.Message);
                        await _pipelineProcessor.ProcessContentAsync(_pendingEmail, update.Message, botClient, cancellationToken);
                        _logger.LogInformation("Created pending email from chat {ChatId}. SenderId: {SenderId}", update.Message.Chat.Id, _pendingEmail.SenderId);
                        _progressDisplayed = false; // сбрасываем флаг для нового сеанса
                    }
                    else
                    {
                        // Обновляем pending email, добавляя контент из нового сообщения (все типы содержимого будут обработаны внутри EmailPipelineProcessor).
                        await _pipelineProcessor.ProcessContentAsync(_pendingEmail, update.Message, botClient, cancellationToken);
                        _logger.LogInformation("Updated pending email with new content, chat {ChatId}", update.Message.Chat.Id);
                    }

                    // Если для отправителя отсутствует привязка, показываем клавиатуру выбора компаний
                    if (_pendingEmail.SenderId != 0 &&
                        (!_companyBindingService.TryGetCompany(_pendingEmail.SenderId, out string binding) || string.IsNullOrWhiteSpace(binding)))
                    {
                        if (!_progressDisplayed)
                        {
                            InlineKeyboardMarkup letterKeyboard = _keyboardGenerator.GenerateLetterKeyboard(_companyListService.GetCompanies());
                            await botClient.SendMessage(
                                update.Message.Chat.Id,
                                "Sender not bound. Choose a starting letter to filter companies, or use /bind <organization> for manual binding.",
                                parseMode: ParseMode.None,
                                disableNotification: false,
                                replyMarkup: letterKeyboard,
                                cancellationToken: cancellationToken);
                            // Пока остается текущая клавиатура для выбора привязки, без изменения _progressDisplayed.
                        }
                    }
                    else
                    {
                        // Если привязка уже есть, запускаем симуляцию прогресса и показываем confirmation клавиатуру один раз для всей группы.
                        if (!_progressDisplayed)
                        {
                            var progressMessage = await botClient.SendMessage(
                                update.Message.Chat.Id,
                                "Processing...",
                                parseMode: ParseMode.None,
                                disableNotification: false,
                                replyMarkup: null,
                                cancellationToken: cancellationToken);

                            await _progressKeyboardService.SimulateProgressAsync(botClient, update.Message.Chat.Id, progressMessage.MessageId, cancellationToken);

                            InlineKeyboardMarkup confirmationKeyboard = _keyboardGenerator.GenerateConfirmationKeyboard();
                            await botClient.EditMessageReplyMarkup(update.Message.Chat.Id, progressMessage.MessageId, confirmationKeyboard, null, cancellationToken);
                            _progressDisplayed = true;
                        }
                    }
                }
            }
            else if (update.CallbackQuery != null)
            {
                string data = update.CallbackQuery.Data ?? string.Empty;
                if (data.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
                {
                    await _adminCommandHandler.ProcessCallbackAsync(botClient, update.CallbackQuery, cancellationToken);
                }
                else if (data.StartsWith("bind", StringComparison.OrdinalIgnoreCase))
                {
                    await _bindCommandHandler.ProcessCallbackAsync(botClient, update.CallbackQuery, cancellationToken);
                }
                else if (data.StartsWith("unbind", StringComparison.OrdinalIgnoreCase))
                {
                    await _unbindCommandHandler.ProcessCallbackAsync(botClient, update.CallbackQuery, cancellationToken);
                }
                else if (data.StartsWith("letter:", StringComparison.OrdinalIgnoreCase))
                {
                    string letter = data.Substring("letter:".Length).Trim();
                    InlineKeyboardMarkup keyboard = _keyboardGenerator.GenerateCompaniesKeyboardForLetter(_companyListService.GetCompanies(), letter);
                    await botClient.EditMessageReplyMarkup(update.CallbackQuery.Message.Chat.Id,
                        update.CallbackQuery.Message.MessageId,
                        keyboard,
                        null,
                        cancellationToken);
                    await botClient.AnswerCallbackQuery(
                        update.CallbackQuery.Id,
                        text: null,
                        showAlert: false,
                        url: null,
                        cacheTime: null,
                        cancellationToken: cancellationToken);
                }
                else if (data.Equals("send", StringComparison.OrdinalIgnoreCase))
                {
                    if (_pendingEmail != null)
                    {
                        _bindingEnricher.EnrichEmailBinding(_pendingEmail);
                        await _emailSender.SendEmailAsync(_pendingEmail, cancellationToken);
                        _pendingEmail = null;
                        _progressDisplayed = false;
                        await botClient.SendMessage(
                            update.CallbackQuery.Message.Chat.Id,
                            "Email sent successfully.",
                            parseMode: ParseMode.None,
                            disableNotification: false,
                            replyMarkup: null,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.AnswerCallbackQuery(
                            update.CallbackQuery.Id,
                            text: "No pending email found.",
                            showAlert: false,
                            url: null,
                            cacheTime: null,
                            cancellationToken: cancellationToken);
                    }
                }
                else if (data.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                {
                    _pendingEmail = null;
                    _progressDisplayed = false;
                    await botClient.SendMessage(
                        update.CallbackQuery.Message.Chat.Id,
                        "Email sending canceled.",
                        parseMode: ParseMode.None,
                        disableNotification: false,
                        replyMarkup: null,
                        cancellationToken: cancellationToken);
                    await botClient.AnswerCallbackQuery(
                        update.CallbackQuery.Id,
                        text: null,
                        showAlert: false,
                        url: null,
                        cacheTime: null,
                        cancellationToken: cancellationToken);
                }
                else if (data.StartsWith("bind:", StringComparison.OrdinalIgnoreCase))
                {
                    // Обработка выбора конкретной компании: "bind:CompanyName"
                    string companyName = data.Substring("bind:".Length).Trim();
                    if (_pendingEmail != null)
                    {
                        _companyBindingService.BindCompany(_pendingEmail.SenderId, companyName);
                        _bindingEnricher.EnrichEmailBinding(_pendingEmail);
                        InlineKeyboardMarkup confirmationKeyboard = _keyboardGenerator.GenerateConfirmationKeyboard();
                        await botClient.EditMessageReplyMarkup(
                            update.CallbackQuery.Message.Chat.Id,
                            update.CallbackQuery.Message.MessageId,
                            confirmationKeyboard,
                            null, cancellationToken);
                        await botClient.AnswerCallbackQuery(
                            update.CallbackQuery.Id,
                            text: $"Bound to {companyName}. Press 'Send' to confirm.",
                            showAlert: false,
                            url: null,
                            cacheTime: null,
                            cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    _logger.LogWarning("Unknown callback data: {Data}", data);
                }
            }
        }

        private EmailMessage CreateEmailMessageFromUpdate(Message msg)
        {
            EmailMessage email = new EmailMessage();
            // Заполняем данные отправителя
            if (msg.ForwardFrom != null)
            {
                email.SenderId = msg.ForwardFrom.Id;
                email.SenderUsername = msg.ForwardFrom.Username;
                email.SenderFullName = $"{msg.ForwardFrom.FirstName} {msg.ForwardFrom.LastName}".Trim();
            }
            else
            {
                email.SenderId = msg.From.Id;
                email.SenderUsername = msg.From.Username;
                email.SenderFullName = $"{msg.From.FirstName} {msg.From.LastName}".Trim();
            }
            email.SentDate = msg.Date;
            email.Subject = $"Пересланные сообщения от {email.SenderUsername ?? email.SenderId.ToString()}";
            // Инициализируем Body с информацией об отправителе
            string header = $"<strong>Информация об отправителе:</strong><br/>" +
                            $"ID: {email.SenderId}<br/>" +
                            $"Username: {email.SenderUsername}<br/>" +
                            $"Имя: {email.SenderFullName}<br/>" +
                            $"Дата: {msg.Date}<br/><hr/>";
            email.Body = header;
            return email;
        }
    }
}
