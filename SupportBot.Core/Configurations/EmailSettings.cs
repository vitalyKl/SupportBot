namespace SupportBot.Core.Configurations
{
    public class EmailSettings
    {
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderPassword { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
        // Дополнительные поля, например, для выбора сервиса отправки, можно добавить здесь.
    }
}
