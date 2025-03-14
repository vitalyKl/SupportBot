using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using TelegramEmailBot.Models;

namespace TelegramEmailBot.Services
{
    public class EmailSender
    {
        private readonly string _gmailUser;
        private readonly string _gmailAppPassword;
        private readonly string _recipientEmail;
        private readonly CompanyBindingService _companyBindingService;

        public EmailSender(string gmailUser, string gmailAppPassword, string recipientEmail, CompanyBindingService companyBindingService)
        {
            _gmailUser = gmailUser;
            _gmailAppPassword = gmailAppPassword;
            _recipientEmail = recipientEmail;
            _companyBindingService = companyBindingService;
        }

        public async Task SendEmailAsync(List<ForwardedMessage> messages)
        {
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("Нет сообщений для отправки", nameof(messages));

            // Если для первого сообщения установлена привязка (пример: компания), используем её название как тему.
            string subject;
            if (messages[0].SenderId.HasValue && _companyBindingService.TryGetCompany(messages[0].SenderId.Value, out string company))
            {
                subject = company;
            }
            else
            {
                // Если привязки нет, формируем стандартную тему.
                if (messages.Count == 1)
                {
                    var firstLine = messages[0].SenderInfo?
                        .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault() ?? "отправитель неизвестен";
                    subject = $"Пересланное сообщение от {firstLine}";
                }
                else
                {
                    subject = $"Пересланные сообщения ({messages.Count})";
                }
            }

            // Формирование единого тела письма: plain-текст и HTML.
            var plainBuilder = new StringBuilder();
            var htmlBuilder = new StringBuilder();

            // Вывод информации об отправителе (берется из первого сообщения)
            string senderInfo = messages[0].SenderInfo ?? string.Empty;
            plainBuilder.AppendLine("Отправитель:");
            plainBuilder.AppendLine(senderInfo);
            plainBuilder.AppendLine();
            htmlBuilder.AppendLine("<html><body style='font-family:Arial, sans-serif;'>");
            htmlBuilder.Append("<p><strong>Отправитель:</strong><br/>");
            htmlBuilder.Append(WebUtility.HtmlEncode(senderInfo ?? string.Empty).Replace("\n", "<br/>"));
            htmlBuilder.AppendLine("</p><hr/>");

            // Коллекции для встраивания изображений и добавления остальных вложений.
            var inlineImages = new List<(string ContentId, AttachmentData Data)>();
            var otherAttachments = new List<AttachmentData>();

            foreach (var msg in messages)
            {
                // Если есть текст, добавляем его.
                if (!string.IsNullOrEmpty(msg.Text))
                {
                    string encodedText = WebUtility.HtmlEncode(msg.Text ?? string.Empty).Replace("\n", "<br/>");
                    htmlBuilder.AppendFormat("<p>{0}</p>", encodedText);
                    plainBuilder.AppendLine(msg.Text);
                    plainBuilder.AppendLine();
                }

                // Обработка вложений.
                if (msg.Attachments != null && msg.Attachments.Any())
                {
                    foreach (var att in msg.Attachments)
                    {
                        // Если MIME-тип указывает на изображение, встраиваем его inline.
                        if (!string.IsNullOrEmpty(att.MimeType) && att.MimeType.StartsWith("image/"))
                        {
                            string contentId = Guid.NewGuid().ToString();
                            inlineImages.Add((contentId, att));
                            htmlBuilder.AppendFormat("<p><img src='cid:{0}' alt='{1}' /></p>", contentId, WebUtility.HtmlEncode(att.FileName));
                            plainBuilder.AppendLine($"[изображение: {att.FileName}]");
                            plainBuilder.AppendLine();
                        }
                        else
                        {
                            // Остальные файлы (например, xlsx, pdf) добавляем как вложения.
                            otherAttachments.Add(att);
                            plainBuilder.AppendLine($"[вложение: {att.FileName}]");
                            plainBuilder.AppendLine();
                            htmlBuilder.AppendFormat("<p>[вложение: {0}]</p>", WebUtility.HtmlEncode(att.FileName));
                        }
                    }
                }
            }
            htmlBuilder.AppendLine("</body></html>");

            string plainBody = plainBuilder.ToString();
            string htmlBody = htmlBuilder.ToString();

            // Создаем сообщение для отправки.
            MailMessage mailMessage = new MailMessage
            {
                From = new MailAddress(_gmailUser),
                Subject = subject,
                Body = plainBody,
                IsBodyHtml = true
            };
            mailMessage.To.Add(_recipientEmail);

            // Создаем AlternateView для HTML-версии письма.
            AlternateView htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);

            // Добавляем изображения как inline-ресурсы.
            foreach (var img in inlineImages)
            {
                var ms = new MemoryStream(img.Data.FileBytes);
                LinkedResource inlineResource = new LinkedResource(ms, img.Data.MimeType)
                {
                    ContentId = img.ContentId,
                    TransferEncoding = TransferEncoding.Base64
                };
                htmlView.LinkedResources.Add(inlineResource);
            }
            mailMessage.AlternateViews.Add(htmlView);

            // ВАЖНО: Для остальных вложений (не изображений) задаем ContentDisposition.Inline = false,
            // чтобы убедиться, что они прикреплены как файлы, а не отображаются inline.
            foreach (var att in otherAttachments)
            {
                var ms = new MemoryStream(att.FileBytes);
                Attachment attachment = new Attachment(ms, att.FileName, att.MimeType);
                attachment.ContentDisposition.Inline = false;
                mailMessage.Attachments.Add(attachment);
            }

            // Отправляем письмо через SMTP (например, Gmail).
            using (var smtpClient = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_gmailUser, _gmailAppPassword),
                EnableSsl = true
            })
            {
                await smtpClient.SendMailAsync(mailMessage);
            }
        }
    }
}
