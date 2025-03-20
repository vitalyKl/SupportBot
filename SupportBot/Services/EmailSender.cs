using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
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

        public async Task SendEmailAsync(EmailMessage email, CancellationToken cancellationToken)
        {
            // Заполняем поле From, используя данные из конфигурации.
            email.From = new MailAddress(_gmailUser, "Bot Sender");

            // Очищаем список получателей и устанавливаем получателя из конфигурации.
            email.To.Clear();
            email.To.Add(_recipientEmail);

            // Обновляем тело письма из объединённого контента.
            email.IsBodyHtml = true;

            using (SmtpClient client = new SmtpClient("smtp.gmail.com", 587))
            {
                client.EnableSsl = true;
                client.Credentials = new System.Net.NetworkCredential(_gmailUser, _gmailAppPassword);
                await client.SendMailAsync(email, cancellationToken);
            }
        }

    }
}
