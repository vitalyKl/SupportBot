namespace TelegramEmailBot.Models
{
    public class AttachmentData
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] FileBytes { get; set; } = System.Array.Empty<byte>();
        public string MimeType { get; set; } = string.Empty;
    }
}
