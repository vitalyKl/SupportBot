namespace TelegramEmailBot.Models
{
    public class AttachmentData
    {
        public string FileName { get; set; }
        public byte[] FileBytes { get; set; }
        public string MimeType { get; set; }
    }
}
