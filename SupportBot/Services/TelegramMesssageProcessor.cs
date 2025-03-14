using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramEmailBot.Models;

namespace TelegramEmailBot.Services
{
    public class TelegramMessageProcessor
    {
        private readonly string _botToken;

        public TelegramMessageProcessor(string botToken)
        {
            _botToken = botToken;
        }

        public async Task<ForwardedMessage> ProcessMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var forwarded = new ForwardedMessage
            {
                MessageId = message.MessageId,
                Timestamp = new DateTimeOffset(message.Date),
                Text = !string.IsNullOrEmpty(message.Text) ? message.Text : message.Caption
            };

            // Формируем информацию об отправителе и сохраняем SenderId (если есть)
            string senderInfo = string.Empty;
            if (message.ForwardFrom != null)
            {
                var user = message.ForwardFrom;
                senderInfo += $"Имя: {user.FirstName} {user.LastName}\n";
                senderInfo += $"Username: {(string.IsNullOrEmpty(user.Username) ? "не указан" : "@" + user.Username)}\n";
                senderInfo += $"ID: {user.Id}\n";
                forwarded.SenderId = user.Id;
            }
            if (message.Contact != null)
            {
                senderInfo += $"Телефон: {message.Contact.PhoneNumber}\n";
            }
            if (!string.IsNullOrEmpty(message.ForwardSenderName))
            {
                senderInfo += $"Отправитель: {message.ForwardSenderName}\n";
            }
            forwarded.SenderInfo = senderInfo;

            // Обработка фотографий
            if (message.Photo != null && message.Photo.Length > 0)
            {
                var bestPhoto = message.Photo.OrderByDescending(p => p.FileSize ?? 0).First();
                try
                {
                    var fileInfo = await botClient.GetFileAsync(bestPhoto.FileId, cancellationToken);
                    string fileUrl = $"https://api.telegram.org/file/bot{_botToken}/{fileInfo.FilePath}";
                    using HttpClient httpClient = new HttpClient();
                    var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                    forwarded.Attachments.Add(new AttachmentData
                    {
                        FileName = $"photo_{message.MessageId}.jpg",
                        FileBytes = fileBytes,
                        MimeType = "image/jpeg"
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при скачивании фото: {ex.Message}");
                }
            }

            // Обработка документов (включая xlsx и любые другие)
            if (message.Document != null)
            {
                try
                {
                    var fileInfo = await botClient.GetFileAsync(message.Document.FileId, cancellationToken);
                    string fileUrl = $"https://api.telegram.org/file/bot{_botToken}/{fileInfo.FilePath}";
                    using HttpClient httpClient = new HttpClient();
                    var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                    forwarded.Attachments.Add(new AttachmentData
                    {
                        FileName = message.Document.FileName,
                        FileBytes = fileBytes,
                        MimeType = message.Document.MimeType
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при скачивании документа: {ex.Message}");
                }
            }

            return forwarded;
        }
    }
}
