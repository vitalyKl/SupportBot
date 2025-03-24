using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using SupportBot.Core.Configurations;
using SupportBot.Core.Entities;
using SupportBot.Core.Interfaces.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Net.Mail;

namespace SupportBot.Services
{
    public class EmailSendingService : IEmailSendingService
    {
        private readonly EmailSettings _emailSettings;
        private readonly string _botToken;

        public EmailSendingService(IOptions<EmailSettings> emailOptions, IOptions<BotSettings> botOptions)
        {
            _emailSettings = emailOptions.Value;
            _botToken = botOptions.Value.BotToken;
        }

        public async Task SendEmailAsync(EmailMessage emailMessage)
        {
            if (emailMessage is null)
                throw new ArgumentNullException(nameof(emailMessage));

            string subject = !string.IsNullOrWhiteSpace(emailMessage.BoundCompany)
                ? $"{emailMessage.BoundCompany}"
                : (string.IsNullOrWhiteSpace(emailMessage.SenderFirstName)
                    ? $"Пересланные сообщения от {emailMessage.SenderId}"
                    : $"Пересланные сообщения от {emailMessage.SenderFirstName}");

            var sb = new StringBuilder();
            sb.Append("<html><body>");
            sb.Append("<h3>Информация об отправителе:</h3>");
            if (!string.IsNullOrWhiteSpace(emailMessage.SenderFirstName))
                sb.Append($"<p>Имя: {WebUtility.HtmlEncode(emailMessage.SenderFirstName)}</p>");
            if (string.IsNullOrWhiteSpace(emailMessage.BoundCompany))
            {
                if (!string.IsNullOrWhiteSpace(emailMessage.SenderId))
                    sb.Append($"<p>ID: {WebUtility.HtmlEncode(emailMessage.SenderId)}</p>");
                if (!string.IsNullOrWhiteSpace(emailMessage.SenderUserName))
                    sb.Append($"<p>Username: {WebUtility.HtmlEncode(emailMessage.SenderUserName)}</p>");
            }
            sb.Append($"<p>Дата первого сообщения: {emailMessage.FirstMessageDate}</p>");
            sb.Append("<hr/>");

            var linkedResources = new List<LinkedResource>();
            var attachments = new List<Attachment>();

            // Создаем экземпляр TelegramBotClient для скачивания файлов.
            var telegramClient = new TelegramBotClient(_botToken);
            int imageCounter = 1;

            using var httpClient = new HttpClient();

            foreach (var block in emailMessage.Blocks)
            {
                if (block.Type == EmailMessageBlock.BlockType.Text)
                {
                    sb.Append($"<p>{WebUtility.HtmlEncode(block.Content)}</p>");
                }
                else if (block.Type == EmailMessageBlock.BlockType.Photo && !string.IsNullOrWhiteSpace(block.FileId))
                {
                    try
                    {
                        var file = await telegramClient.GetFileAsync(block.FileId);
                        string url = $"https://api.telegram.org/file/bot{_botToken}/{file.FilePath}";
                        byte[] fileBytes = await httpClient.GetByteArrayAsync(url);

                        var contentId = $"image{imageCounter}";
                        imageCounter++;

                        using var msForImage = new MemoryStream(fileBytes);
                        var lr = new LinkedResource(new MemoryStream(fileBytes), MediaTypeNames.Image.Jpeg)
                        {
                            ContentId = contentId,
                            TransferEncoding = TransferEncoding.Base64
                        };
                        linkedResources.Add(lr);

                        sb.Append($"<p><img src=\"cid:{contentId}\" alt=\"Image\" style=\"max-width:600px;\"/></p>");
                    }
                    catch (Exception ex)
                    {
                        sb.Append($"<p>Ошибка загрузки изображения: {WebUtility.HtmlEncode(ex.Message)}</p>");
                    }
                }
                else if (block.Type == EmailMessageBlock.BlockType.Document && !string.IsNullOrWhiteSpace(block.FileId))
                {
                    try
                    {
                        var file = await telegramClient.GetFile(block.FileId);
                        string url = $"https://api.telegram.org/file/bot{_botToken}/{file.FilePath}";
                        byte[] fileBytes = await httpClient.GetByteArrayAsync(url);
                        string fileName = string.IsNullOrWhiteSpace(block.Content) ? "document" : block.Content;
                        string mimeType = GetMimeType(fileName);

                        // Создаем новый MemoryStream из массива байтов
                        var attachmentStream = new MemoryStream(fileBytes);
                        var attachment = new Attachment(attachmentStream, fileName, mimeType);
                        attachments.Add(attachment);

                        sb.Append($"<p>Прикреплен документ: {WebUtility.HtmlEncode(fileName)}</p>");
                    }
                    catch (Exception ex)
                    {
                        sb.Append($"<p>Ошибка загрузки документа: {WebUtility.HtmlEncode(ex.Message)}</p>");
                    }
                }

            }

            sb.Append("</body></html>");
            string htmlBody = sb.ToString();
            AlternateView avHtml = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);
            foreach (var lr in linkedResources)
                avHtml.LinkedResources.Add(lr);

            MailMessage mail = new()
            {
                From = new MailAddress(_emailSettings.SenderEmail),
                Subject = subject,
                IsBodyHtml = true
            };
            mail.To.Add(_emailSettings.RecipientEmail);
            mail.AlternateViews.Add(avHtml);
            foreach (var att in attachments)
                mail.Attachments.Add(att);

            try
            {
                using var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword),
                    EnableSsl = true
                };
                await smtp.SendMailAsync(mail);
            }
            catch (SmtpException smtpEx)
            {
                Console.WriteLine($"SMTP Exception: {smtpEx.Message}");
                if (smtpEx.InnerException != null)
                    Console.WriteLine($"Inner Exception: {smtpEx.InnerException.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при отправке почты: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                throw;
            }
        }

        private string GetMimeType(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                _ => "application/octet-stream",
            };
        }
    }
}
