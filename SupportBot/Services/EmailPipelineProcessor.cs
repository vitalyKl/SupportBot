using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Net.Mime;
using TelegramEmailBot.Models;

namespace TelegramEmailBot.Services
{
    public class EmailPipelineProcessor
    {
        /// <summary>
        /// Обрабатывает входящее сообщение и обновляет EmailMessage в зависимости от типа контента:
        /// - текст или caption дописываются в Body,
        /// - фотографии скачиваются и встраиваются как LinkedResource в AlternateView,
        /// - документы скачиваются и добавляются как Attachment.
        /// </summary>
        public async Task ProcessContentAsync(EmailMessage email, Message msg, ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            // Обработка текстового содержимого:
            if (!string.IsNullOrWhiteSpace(msg.Text))
            {
                email.Body += "<br/>" + msg.Text;
            }
            else if (!string.IsNullOrWhiteSpace(msg.Caption))
            {
                email.Body += "<br/>" + msg.Caption;
            }
            // Если сообщение не содержит текст, но содержит документ – добавляем placeholder в тело письма:
            else if (msg.Document != null)
            {
                email.Body += $"<br/><p>[Документ: {msg.Document.FileName}]</p>";
            }

            // Обработка фотографий (встроенное изображение):
            if (msg.Photo != null && msg.Photo.Length > 0)
            {
                try
                {
                    var bestPhoto = msg.Photo.Last();
                    var file = await botClient.GetFile(bestPhoto.FileId, cancellationToken);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Скачиваем фото (синхронно обернутый в Task.Run — без суффикса Async)
                        await Task.Run(() => botClient.DownloadFile(file.FilePath, ms), cancellationToken);
                        _ = ms.Position;
                        byte[] photoBytes = ms.ToArray();
                        // Создаем новый поток для LinkedResource (чтобы он не закрывался)
                        MemoryStream photoStream = new MemoryStream(photoBytes);
                        LinkedResource imageResource = new LinkedResource(photoStream, "image/jpeg")
                        {
                            ContentId = Guid.NewGuid().ToString()
                        };
                        // Добавляем HTML для встроенного изображения
                        email.Body += $"<br/><img src=\"cid:{imageResource.ContentId}\" alt=\"Фото\"/><br/>";

                        // Если AlternateView уже существует, добавляем LinkedResource, иначе создаем новый AlternateView
                        AlternateView av;
                        if (email.AlternateViews.Count == 0)
                        {
                            av = AlternateView.CreateAlternateViewFromString(email.Body, null, "text/html");
                            email.AlternateViews.Add(av);
                        }
                        else
                        {
                            av = email.AlternateViews[0];
                        }
                        av.LinkedResources.Add(imageResource);
                    }
                }
                catch (Exception ex)
                {
                    // Логирование ошибки можно добавить, если необходимо
                    // Например: _logger.LogError(ex, "Ошибка обработки фото");
                }
            }

            // Обработка документов (например, xlsx)
            if (msg.Document != null)
            {
                try
                {
                    var doc = msg.Document;
                    var file = await botClient.GetFile(doc.FileId, cancellationToken);
                    using (MemoryStream msDoc = new MemoryStream())
                    {
                        await Task.Run(() => botClient.DownloadFile(file.FilePath, msDoc), cancellationToken);
                        byte[] docBytes = msDoc.ToArray();
                        MemoryStream docStream = new MemoryStream(docBytes);
                        Attachment attachment = new Attachment(docStream, doc.FileName, doc.MimeType);
                        // Файл как приложение – не влияет на Body
                        email.Attachments.Add(attachment);
                    }
                }
                catch (Exception ex)
                {
                    // Логирование ошибки
                }
            }
        }
    }
}
