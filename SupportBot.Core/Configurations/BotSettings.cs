namespace SupportBot.Core.Configurations
{
    public class BotSettings
    {
        // Инициализация по умолчанию позволяет избежать предупреждений.
        public string BotToken { get; set; } = string.Empty;
        public string EncryptionKey { get; set; } = string.Empty;
    }
}
