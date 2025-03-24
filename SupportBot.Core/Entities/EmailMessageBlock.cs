namespace SupportBot.Core.Entities
{
    public class EmailMessageBlock
    {
        public enum BlockType { Text, Photo, Document }
        public BlockType Type { get; set; }
        // Для текстовых блоков – текст; для фото – подпись (если есть)
        public string Content { get; set; } = string.Empty;
        // Для фото или документов – файл (Telegram file_id)
        public string? FileId { get; set; }
    }
}
