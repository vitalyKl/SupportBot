using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramEmailBot.Handlers.Keyboards
{
    public class KeyboardGenerator
    {
        public InlineKeyboardMarkup GenerateAdminPanelKeyboard()
        {
            var buttons = new[]
            {
                new [] { InlineKeyboardButton.WithCallbackData("Отвязать пользователя", "admin_unbind_list") },
                new [] { InlineKeyboardButton.WithCallbackData("Удалить компанию", "admin_delete_company_list") },
                new [] { InlineKeyboardButton.WithCallbackData("Отмена", "admin_cancel") }
            };
            return new InlineKeyboardMarkup(buttons);
        }

        public InlineKeyboardMarkup GenerateLetterKeyboard(IEnumerable<string> companies)
        {
            var letters = companies
                .Select(c => c.Trim().Substring(0, 1).ToUpper())
                .Distinct()
                .OrderBy(l => l)
                .ToList();

            var rows = new List<InlineKeyboardButton[]>();
            foreach (var letter in letters)
            {
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData(letter, $"letter:{letter}") });
            }
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("All", "letter:ALL") });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Отмена", "cancel") });
            return new InlineKeyboardMarkup(rows);
        }

        public InlineKeyboardMarkup GenerateCompaniesKeyboardForLetter(IEnumerable<string> companies, string letter)
        {
            IEnumerable<string> filtered = letter.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                ? companies
                : companies.Where(c => c.StartsWith(letter, StringComparison.OrdinalIgnoreCase));
            var rows = filtered.Select(c => new[] { InlineKeyboardButton.WithCallbackData(c, $"bind:{c}") }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "letters") });
            return new InlineKeyboardMarkup(rows);
        }

        public InlineKeyboardMarkup GenerateConfirmationKeyboard()
        {
            var buttons = new[]
            {
                new [] { InlineKeyboardButton.WithCallbackData("Send", "send") },
                new [] { InlineKeyboardButton.WithCallbackData("Cancel", "cancel") }
            };
            return new InlineKeyboardMarkup(buttons);
        }

        // Добавляем необходимый метод для обработки admin callback'ов.
        public async Task ProcessAdminCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                text: "Admin command not implemented.",
                showAlert: false,
                url: null,
                cacheTime: null,
                cancellationToken: cancellationToken);
        }
    }
}
