namespace TelegramEmailBot.Models
{
    public class EmailOptions
    {
        public string GmailUser { get; set; } = string.Empty;
        public string GmailAppPassword { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
    }
}
