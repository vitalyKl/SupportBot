using System.Net.Mail;
using System.Net;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;

public class MyUpdateHandler : IUpdateHandler
{
    // Настройки email
    private static readonly string GmailUser = "fmonitoringbot@gmail.com";
    private static readonly string GmailAppPassword = "aoru xwsk clwd lfjm";  // пароль приложения
    private static readonly string RecipientEmail = "support@fmg24.by"; // куда отправлять email
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Нас интересуют только сообщения
        if (update.Type != UpdateType.Message)
            return;

        var message = update.Message;
        if (message == null || string.IsNullOrEmpty(message.Text))
            return;

        // Проверка, что сообщение пересланное (ForwardFrom не null)
        if (message.ForwardFrom != null)
        {
            string senderFirstName = message.ForwardFrom.FirstName ?? "";
            string senderLastName = message.ForwardFrom.LastName ?? "";
            string senderInfo = (senderFirstName + " " + senderLastName).Trim();
            if (string.IsNullOrEmpty(senderInfo))
                senderInfo = "Неизвестный отправитель";

            string emailSubject = $"Пересланное сообщение от {senderInfo}";
            string emailBody = $"Сообщение:\n\n{message.Text}\n\nЧат Telegram (ID): {message.Chat.Id}";

            try
            {
                await SendEmailAsync(emailSubject, emailBody);
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Email успешно отправлен.",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Ошибка при отправке email: {ex.Message}",
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Пожалуйста, пришлите пересланное сообщение.",
                cancellationToken: cancellationToken);
        }
    }

    // Обработка ошибок, теперь с дополнительным параметром errorSource
    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource errorSource, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка обновления: {exception.Message}");
        Console.WriteLine($"Источник ошибки: {errorSource}");
        return Task.CompletedTask;
    }

    // Асинхронная отправка email через Gmail
    private static async Task SendEmailAsync(string subject, string body)
    {
        using (var smtpClient = new SmtpClient("smtp.gmail.com", 587))
        {
            smtpClient.Credentials = new NetworkCredential(GmailUser, GmailAppPassword);
            smtpClient.EnableSsl = true;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(GmailUser),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            mailMessage.To.Add(RecipientEmail);
            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}