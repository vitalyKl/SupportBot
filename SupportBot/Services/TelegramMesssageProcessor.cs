using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramEmailBot.Models;
using TelegramEmailBot.Models.Interfaces;

namespace TelegramEmailBot.Services
{
    public class TelegramMessageProcessor
    {
        private readonly string _botToken;

        public TelegramMessageProcessor(string botToken)
        {
            _botToken = botToken;
        }

        public async Task<ForwardedMessage> ProcessMessageAsync(ITelegramBotClient botClient, IIncomingMessage message, CancellationToken cancellationToken)
        {
            var forwarded = new ForwardedMessage
            {
                MessageId = message.MessageId,
                Timestamp = new DateTimeOffset(message.Date),
                Text = !string.IsNullOrEmpty(message.Text) ? message.Text : message.Caption
            };

            if (message.ForwardFrom != null)
            {
                var user = message.ForwardFrom;
                forwarded.SenderInfo = $"Имя: {user.FirstName} {user.LastName}\n" +
                                         $"Username: {(string.IsNullOrEmpty(user.Username) ? "не указан" : "@" + user.Username)}\n" +
                                         $"ID: {user.Id}\n";
                forwarded.SenderId = user.Id;
            }

            // Обработка фотографий
            if (message.Photo != null && message.Photo.Any())
            {
                var bestPhoto = message.Photo.OrderByDescending(p => p.FileSize ?? 0).First();
                try
                {
                    var fileInfo = await botClient.GetFile(bestPhoto.FileId, cancellationToken);
                    string fileUrl = $"https://api.telegram.org/file/bot{_botToken}/{fileInfo.FilePath}";
                    using (HttpClient httpClient = new HttpClient())
                    {
                        byte[] fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                        forwarded.Attachments.Add(new AttachmentData
                        {
                            FileName = $"photo_{message.MessageId}.jpg",
                            FileBytes = fileBytes,
                            MimeType = "image/jpeg"
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при скачивании фото: {ex.Message}");
                }
            }

            // Обработка документов (например, xlsx, pdf)
            if (message.Document != null)
            {
                try
                {
                    var fileInfo = await botClient.GetFile(message.Document.FileId, cancellationToken);
                    string fileUrl = $"https://api.telegram.org/file/bot{_botToken}/{fileInfo.FilePath}";
                    using (HttpClient httpClient = new HttpClient())
                    {
                        byte[] fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                        forwarded.Attachments.Add(new AttachmentData
                        {
                            FileName = message.Document.FileName,
                            FileBytes = fileBytes,
                            MimeType = message.Document.MimeType
                        });
                    }
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
