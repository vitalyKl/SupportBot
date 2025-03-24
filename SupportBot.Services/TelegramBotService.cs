using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using SupportBot.Core.Configurations;
using SupportBot.Core.Interfaces.Services;
using SupportBot.Core.Entities;

namespace SupportBot.Services
{
    public class TelegramBotService : ITelegramBotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly IEmailSupplementService _emailSupplementService;
        private readonly IEmailSendingService _emailSendingService;
        private readonly IInlineKeyboardService _inlineKeyboardService;
        private readonly ICompanyBindingService _companyBindingService;
        private readonly ICompanyService _companyService;
        private bool _keyboardSent = false;

        public TelegramBotService(
            IOptions<BotSettings> options,
            IEmailSupplementService emailSupplementService,
            IEmailSendingService emailSendingService,
            IInlineKeyboardService inlineKeyboardService,
            ICompanyBindingService companyBindingService,
            ICompanyService companyService)
        {
            ArgumentNullException.ThrowIfNull(options, nameof(options));
            var botToken = options.Value.BotToken;
            if (string.IsNullOrWhiteSpace(botToken))
                throw new ArgumentException("Bot token is missing in configuration.");
            _botClient = new TelegramBotClient(botToken);
            _emailSupplementService = emailSupplementService;
            _emailSendingService = emailSendingService;
            _inlineKeyboardService = inlineKeyboardService;
            _companyBindingService = companyBindingService;
            _companyService = companyService;
        }

        public Task StartAsync()
        {
            _botClient.StartReceiving(
                async (bot, update, cancellationToken) =>
                {
                    Console.WriteLine($"Получено обновление: {update.Type}");

                    // --- Обработка команды /bind ---
                    if (update.Type == UpdateType.Message &&
                        update.Message?.Text != null &&
                        update.Message.Text.StartsWith("/bind", StringComparison.OrdinalIgnoreCase))
                    {
                        var message = update.Message;
                        string companyName = message.Text.Substring(5).Trim(); // текст после /bind
                        if (string.IsNullOrWhiteSpace(companyName))
                        {
                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "Используйте команду: /bind <название компании>",
                                cancellationToken: cancellationToken
                            );
                        }
                        else
                        {
                            // Добавляем компанию, если её ещё нет
                            _companyService.AddCompanyIfNotExists(companyName);

                            // Определяем автора для привязки: если коллекция накопленных сообщений существует, берем оттуда SenderId, иначе message.From
                            string authorId = _emailSupplementService.GetEmailMessage()?.SenderId ?? message.From!.Id.ToString();
                            // Сохраняем привязку
                            _companyBindingService.SaveBinding(authorId, companyName);
                            // Обновляем аккумулированный объект EmailMessage (если он существует)
                            _emailSupplementService.SetBoundCompany(companyName);

                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: $"Компания '{companyName}' добавлена и привязана. Теперь вы можете отправить сообщение.",
                                cancellationToken: cancellationToken
                            );
                            // После команды /bind сразу отправляем клавиатуру подтверждения для накопленных пересланных сообщений.
                            var confirmationKeyboard = _inlineKeyboardService.GenerateConfirmationKeyboard();
                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "Подтвердите отправку email:",
                                replyMarkup: confirmationKeyboard,
                                cancellationToken: cancellationToken
                            );
                            _keyboardSent = true;
                        }
                        return;
                    }
                    // --- Конец обработки команды /bind ---

                    if (update.Type == UpdateType.Message)
                    {
                        var message = update.Message;
                        if (message == null || message.Chat == null)
                        {
                            Console.WriteLine("Warning: message или message.Chat равны null.");
                            return;
                        }

                        // Обработка пересланного сообщения (используем информацию только из ForwardOrigin)
                        if (message.ForwardOrigin != null)
                        {
                            string senderId;
                            string senderUserName;
                            string senderFirstName;

                            if (message.ForwardOrigin is MessageOriginUser originUser)
                            {
                                senderId = originUser.SenderUser.Id.ToString();
                                senderUserName = originUser.SenderUser.Username ?? "";
                                senderFirstName = originUser.SenderUser.FirstName ?? "";
                            }
                            else if (message.ForwardOrigin is MessageOriginHiddenUser originHidden)
                            {
                                senderId = "Hidden";
                                senderUserName = originHidden.SenderUserName;
                                senderFirstName = "";
                            }
                            else if (message.ForwardOrigin is MessageOriginChat originChat)
                            {
                                senderId = originChat.SenderChat.Id.ToString();
                                senderUserName = originChat.SenderChat.Title ?? "";
                                senderFirstName = "";
                            }
                            else if (message.ForwardOrigin is MessageOriginChannel originChannel)
                            {
                                senderId = originChannel.Chat.Id.ToString();
                                senderUserName = originChannel.Chat.Title ?? "";
                                senderFirstName = "";
                            }
                            else
                            {
                                Console.WriteLine("Не удалось определить тип ForwardOrigin.");
                                return;
                            }

                            Console.WriteLine($"Обнаружено пересланное сообщение от {senderUserName} (ID: {senderId}).");

                            _emailSupplementService.SupplementEmailMessage(
                                senderId,
                                senderUserName,
                                senderFirstName,
                                message.Text ?? "",
                                message.Date
                            );

                            // Проверяем, существует ли привязка автора к компании.
                            var binding = _companyBindingService.GetBinding(senderId);
                            if (binding != null)
                            {
                                _emailSupplementService.SetBoundCompany(binding);
                                if (!_keyboardSent)
                                {
                                    var keyboard = _inlineKeyboardService.GenerateConfirmationKeyboard();
                                    await _botClient.SendMessage(
                                        chatId: message.Chat.Id,
                                        text: "Накоплено пересланное сообщение. Подтвердите отправку email:",
                                        replyMarkup: keyboard,
                                        cancellationToken: cancellationToken
                                    );
                                    _keyboardSent = true;
                                }
                            }
                            else
                            {
                                // Если привязки нет – генерируем клавиатуру для выбора буквы, основываясь на наличии компаний
                                var letterKeyboard = _inlineKeyboardService.GenerateLetterKeyboard();
                                await _botClient.SendMessage(
                                    chatId: message.Chat.Id,
                                    text: "Ваш автор не привязан к компании. Выберите букву для поиска:",
                                    replyMarkup: letterKeyboard,
                                    cancellationToken: cancellationToken
                                );
                                _keyboardSent = true;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Полученное сообщение не содержит ForwardOrigin.");
                        }
                    }
                    else if (update.Type == UpdateType.CallbackQuery)
                    {
                        var callback = update.CallbackQuery;
                        if (callback == null)
                            return;

                        Console.WriteLine($"Получен callback: {callback.Data}");

                        if (callback.Data == "send")
                        {
                            var emailMessage = _emailSupplementService.GetEmailMessage();
                            if (emailMessage != null)
                            {
                                await _emailSendingService.SendEmailAsync(emailMessage);
                                await _botClient.AnswerCallbackQuery(callback.Id, "Email отправлен!", cancellationToken: cancellationToken);
                                await _botClient.EditMessageReplyMarkup(
                                    chatId: callback.Message!.Chat.Id,
                                    messageId: callback.Message!.MessageId,
                                    replyMarkup: null,
                                    cancellationToken: cancellationToken
                                );
                                _emailSupplementService.ClearEmailMessage();
                                await _botClient.SendMessage(
                                    chatId: callback.Message!.Chat.Id,
                                    text: "Email отправлен. Готов к получению новых пересланных сообщений.",
                                    cancellationToken: cancellationToken
                                );
                            }
                            else
                            {
                                await _botClient.AnswerCallbackQuery(callback.Id, "Нет накопленных сообщений.", cancellationToken: cancellationToken);
                            }
                            _keyboardSent = false;
                        }
                        else if (callback.Data == "cancel")
                        {
                            _emailSupplementService.ClearEmailMessage();
                            await _botClient.AnswerCallbackQuery(callback.Id, "Отправка отменена.", cancellationToken: cancellationToken);
                            await _botClient.EditMessageReplyMarkup(
                                chatId: callback.Message!.Chat.Id,
                                messageId: callback.Message!.MessageId,
                                replyMarkup: null,
                                cancellationToken: cancellationToken
                            );
                            await _botClient.SendMessage(
                                chatId: callback.Message!.Chat.Id,
                                text: "Отправка отменена. Готов к получению новых пересланных сообщений.",
                                cancellationToken: cancellationToken
                            );
                            _keyboardSent = false;
                        }
                        else if (callback.Data.StartsWith("letter:"))
                        {
                            string letterStr = callback.Data.Substring("letter:".Length);
                            char letter = letterStr[0];
                            var companies = _companyService.GetCompaniesByLetter(letter);
                            var companiesKeyboard = _inlineKeyboardService.GenerateCompaniesKeyboard(letter, companies);

                            await _botClient.EditMessageReplyMarkup(
                                chatId: callback.Message!.Chat.Id,
                                messageId: callback.Message!.MessageId,
                                replyMarkup: companiesKeyboard,
                                cancellationToken: cancellationToken
                            );
                        }
                        else if (callback.Data.Equals("back"))
                        {
                            var letterKeyboard = _inlineKeyboardService.GenerateLetterKeyboard();
                            await _botClient.EditMessageReplyMarkup(
                                chatId: callback.Message!.Chat.Id,
                                messageId: callback.Message!.MessageId,
                                replyMarkup: letterKeyboard,
                                cancellationToken: cancellationToken
                            );
                        }
                        else if (callback.Data.StartsWith("company:"))
                        {
                            string companyName = callback.Data.Substring("company:".Length);
                            var emailMessage = _emailSupplementService.GetEmailMessage();
                            if (emailMessage != null)
                            {
                                string authorId = emailMessage.SenderId;
                                _companyBindingService.SaveBinding(authorId, companyName);
                                _emailSupplementService.SetBoundCompany(companyName);

                                await _botClient.AnswerCallbackQuery(callback.Id, $"Привязка установлена: {companyName}", cancellationToken: cancellationToken);
                                var confirmationKeyboard = _inlineKeyboardService.GenerateConfirmationKeyboard();
                                await _botClient.EditMessageReplyMarkup(
                                    chatId: callback.Message!.Chat.Id,
                                    messageId: callback.Message!.MessageId,
                                    replyMarkup: confirmationKeyboard,
                                    cancellationToken: cancellationToken
                                );
                            }
                        }
                    }
                },
                async (bot, exception, cancellationToken) =>
                {
                    Console.WriteLine($"Ошибка в TelegramBotService: {exception.Message}");
                    await Task.CompletedTask;
                }
            );

            Console.WriteLine("Telegram Bot Service запущен.");
            return Task.CompletedTask;
        }
    }
}
