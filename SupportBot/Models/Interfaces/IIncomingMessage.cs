using System;
using Telegram.Bot.Types;

namespace TelegramEmailBot.Models.Interfaces
{
    public interface IIncomingMessage
    {
        int MessageId { get; }
        DateTime Date { get; }
        // Основной текст сообщения
        string Text { get; }
        // Caption, если сообщение имеет подпись (например, фото)
        string Caption { get; }
        // Контакт (если есть)
        Contact? Contact { get; }
        // Массив фотографий (если есть)
        PhotoSize[] Photo { get; }
        // Документ (если есть)
        Document? Document { get; }
        // Данные о пересылаемом отправителе
        User? ForwardFrom { get; }
        // Имя отправителя в виде строки (если задано)
        string ForwardSenderName { get; }
    }
}
