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
        // Флаг, указывающий, что для текущей группы сообщений уже отправлена inline-клавиатура
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

                    // Обработка команды /bind для ручной привязки компании
                    if (update.Type == UpdateType.Message &&
                        update.Message?.Text != null &&
                        update.Message.Text.StartsWith("/bind", StringComparison.OrdinalIgnoreCase))
                    {
                        var message = update.Message;
                        string companyName = message.Text.Substring(5).Trim();
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
                            _companyService.AddCompanyIfNotExists(companyName);
                            // Если уже накоплено сообщение, используем его SenderId, иначе берём из message.From
                            string authorId = _emailSupplementService.GetEmailMessage()?.SenderId ?? message.From!.Id.ToString();
                            _companyBindingService.SaveBinding(authorId, companyName);
                            _emailSupplementService.SetBoundCompany(companyName);

                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: $"Компания '{companyName}' добавлена и привязана. Теперь вы можете отправить сообщение.",
                                cancellationToken: cancellationToken
                            );
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
                    // Обработка входящих сообщений
                    if (update.Type == UpdateType.Message)
                    {
                        var message = update.Message;
                        if (message == null || message.Chat == null)
                        {
                            Console.WriteLine("Warning: message или message.Chat равны null.");
                            return;
                        }
                        // Обрабатываем только пересланные сообщения – если ForwardOrigin заполнен
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

                            // Если сообщение содержит фото, создаём блок для фото.
                            if (message.Photo != null && message.Photo.Length > 0)
                            {
                                var photo = message.Photo[^1]; // последнее фото (наибольшего размера)
                                var photoBlock = new EmailMessageBlock
                                {
                                    Type = EmailMessageBlock.BlockType.Photo,
                                    FileId = photo.FileId,
                                    Content = "" // Подпись обрабатывается отдельно
                                };
                                _emailSupplementService.SupplementEmailMessageBlock(photoBlock, senderId, senderUserName, senderFirstName, message.Date);
                                // Если у фото есть подпись, добавляем текстовый блок
                                if (!string.IsNullOrWhiteSpace(message.Caption))
                                {
                                    _emailSupplementService.SupplementEmailMessage(senderId, senderUserName, senderFirstName, message.Caption, message.Date);
                                }
                            }
                            // Если сообщение содержит документ (например, Word, PDF и т.д.)
                            else if (message.Document != null)
                            {
                                var doc = message.Document;
                                var docBlock = new EmailMessageBlock
                                {
                                    Type = EmailMessageBlock.BlockType.Document,
                                    FileId = doc.FileId,
                                    Content = doc.FileName // сохраняем имя файла
                                };
                                _emailSupplementService.SupplementEmailMessageBlock(docBlock, senderId, senderUserName, senderFirstName, message.Date);
                                // Если у документа есть подпись (Caption), добавляем её как текстовый блок
                                if (!string.IsNullOrWhiteSpace(message.Caption))
                                {
                                    _emailSupplementService.SupplementEmailMessage(senderId, senderUserName, senderFirstName, message.Caption, message.Date);
                                }
                            }
                            else
                            {
                                // Обрабатываем как текстовое сообщение
                                _emailSupplementService.SupplementEmailMessage(senderId, senderUserName, senderFirstName, message.Text ?? "", message.Date);
                            }

                            // После добавления блоков проверяем наличие привязки
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
                                if (!_keyboardSent)
                                {
                                    // Если нет привязки, выводим клавиатуру выбора буквы из имеющихся компаний
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
