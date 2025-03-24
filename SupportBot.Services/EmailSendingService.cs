using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SupportBot.Core.Configurations;
using SupportBot.Core.Entities;
using SupportBot.Core.Interfaces.Services;

namespace SupportBot.Services
{
    public class EmailSendingService : IEmailSendingService
    {
        private readonly EmailSettings _emailSettings;

        public EmailSendingService(IOptions<EmailSettings> options)
        {
            _emailSettings = options.Value;
        }

        public async Task SendEmailAsync(EmailMessage emailMessage)
        {
            if (emailMessage is null) throw new System.ArgumentNullException(nameof(emailMessage));

            string subject;
            if (!string.IsNullOrWhiteSpace(emailMessage.BoundCompany))
            {
                subject = $"{emailMessage.BoundCompany}";
            }
            else
            {
                subject = string.IsNullOrWhiteSpace(emailMessage.SenderFirstName)
                    ? $"Пересланные сообщения от {emailMessage.SenderId}"
                    : $"Пересланные сообщения от {emailMessage.SenderFirstName}";
            }

            string body = "Информация об отправителе:\n";
            if (!string.IsNullOrWhiteSpace(emailMessage.SenderFirstName))
                body += $"Имя: {emailMessage.SenderFirstName}\n";
            if (string.IsNullOrWhiteSpace(emailMessage.BoundCompany))
            {
                if (!string.IsNullOrWhiteSpace(emailMessage.SenderId))
                    body += $"ID: {emailMessage.SenderId}\n";
                if (!string.IsNullOrWhiteSpace(emailMessage.SenderUserName))
                    body += $"Username: {emailMessage.SenderUserName}\n";
            }
            body += $"Дата первого сообщения: {emailMessage.FirstMessageDate}\n\n";

            foreach (var msg in emailMessage.MessageTexts)
            {
                body += msg + "\n\n";
            }

            MailMessage mail = new()
            {
                From = new MailAddress(_emailSettings.SenderEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            mail.To.Add(_emailSettings.RecipientEmail);

            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword),
                EnableSsl = true
            };

            await smtp.SendMailAsync(mail);
        }
    }
}
