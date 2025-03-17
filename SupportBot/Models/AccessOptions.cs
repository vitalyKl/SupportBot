namespace TelegramEmailBot.Models
{
    public class AccessOptions
    {
        // ID единственного пользователя, которому разрешено использовать бота.
        public long AuthorizedUserId { get; set; }
    }
}
