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
            _gmailUser = gmailUser ?? throw new ArgumentNullException(nameof(gmailUser));
            _gmailAppPassword = gmailAppPassword ?? throw new ArgumentNullException(nameof(gmailAppPassword));
            _recipientEmail = recipientEmail ?? throw new ArgumentNullException(nameof(recipientEmail));
            _companyBindingService = companyBindingService;
        }

        public async Task SendEmailAsync(List<ForwardedMessage> messages)
        {
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("Нет сообщений для отправки", nameof(messages));

            string subject;
            if (messages[0].SenderId.HasValue && _companyBindingService.TryGetCompany(messages[0].SenderId.Value, out string company))
            {
                subject = company;
            }
            else
            {
                subject = messages.Count == 1
                    ? $"Пересланное сообщение от {messages[0].SenderInfo.Split('\n')[0]}"
                    : $"Пересланные сообщения ({messages.Count})";
            }

            var plainBuilder = new StringBuilder();
            var htmlBuilder = new StringBuilder();

            string senderInfo = messages[0].SenderInfo;
            plainBuilder.AppendLine("Отправитель:");
            plainBuilder.AppendLine(senderInfo);
            plainBuilder.AppendLine();
            htmlBuilder.AppendLine("<html><body style='font-family:Arial, sans-serif;'>");
            htmlBuilder.Append("<p><strong>Отправитель:</strong><br/>");
            htmlBuilder.Append(WebUtility.HtmlEncode(senderInfo).Replace("\n", "<br/>"));
            htmlBuilder.AppendLine("</p><hr/>");

            var inlineImages = new List<(string ContentId, AttachmentData Data)>();
            var otherAttachments = new List<AttachmentData>();

            foreach (var msg in messages)
            {
                if (!string.IsNullOrEmpty(msg.Text))
                {
                    string encodedText = WebUtility.HtmlEncode(msg.Text).Replace("\n", "<br/>");
                    htmlBuilder.AppendFormat("<p>{0}</p>", encodedText);
                    plainBuilder.AppendLine(msg.Text);
                    plainBuilder.AppendLine();
                }

                if (msg.Attachments != null && msg.Attachments.Any())
                {
                    foreach (var att in msg.Attachments)
                    {
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

            MailMessage mailMessage = new MailMessage
            {
                From = new MailAddress(_gmailUser),
                Subject = subject,
                Body = plainBody,
                IsBodyHtml = true
            };
            mailMessage.To.Add(_recipientEmail);

            AlternateView htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);
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

            foreach (var att in otherAttachments)
            {
                var ms = new MemoryStream(att.FileBytes);
                Attachment attachment = new Attachment(ms, att.FileName, att.MimeType);
                attachment.ContentDisposition.Inline = false;
                mailMessage.Attachments.Add(attachment);
            }

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
