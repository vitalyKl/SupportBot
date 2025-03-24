using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Generic;

namespace SupportBot.Core.Interfaces.Services
{
    public interface IInlineKeyboardService
    {
        InlineKeyboardMarkup GenerateConfirmationKeyboard();
        InlineKeyboardMarkup GenerateLetterKeyboard();
        // Первый параметр теперь имеет тип char
        InlineKeyboardMarkup GenerateCompaniesKeyboard(char letter, IEnumerable<string> companies);
    }
}
