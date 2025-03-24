using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Generic;

namespace SupportBot.Core.Interfaces.Services
{
    public interface IInlineKeyboardService
    {
        InlineKeyboardMarkup GenerateConfirmationKeyboard();
        InlineKeyboardMarkup GenerateLetterKeyboard();
        InlineKeyboardMarkup GenerateCompaniesKeyboard(char letter, IEnumerable<string> companies);
    }
}
