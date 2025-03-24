using System.Linq;
using Telegram.Bot.Types.ReplyMarkups;
using SupportBot.Core.Interfaces.Services;
using System.Collections.Generic;

namespace SupportBot.Services
{
    public class InlineKeyboardService : IInlineKeyboardService
    {
        private readonly ICompanyService _companyService;

        public InlineKeyboardService(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        public InlineKeyboardMarkup GenerateConfirmationKeyboard()
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("Send", "send"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("Cancel", "cancel")
                }
            });
            return keyboard;
        }

        public InlineKeyboardMarkup GenerateLetterKeyboard()
        {
            var companies = _companyService.GetAllCompanies();
            // Выбираем только те буквы, для которых есть компании.
            var distinctLetters = companies
                                  .Where(c => !string.IsNullOrWhiteSpace(c))
                                  .Select(c => char.ToUpper(c[0]))
                                  .Distinct()
                                  .OrderBy(x => x)
                                  .Select(x => x.ToString())
                                  .ToArray();

            var keyboardButtons = distinctLetters.Select(letter => new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData(letter, "letter:" + letter)
            }).ToArray();

            return new InlineKeyboardMarkup(keyboardButtons);
        }

        public InlineKeyboardMarkup GenerateCompaniesKeyboard(char letter, IEnumerable<string> companies)
        {
            var filtered = companies.Where(c => !string.IsNullOrWhiteSpace(c) && char.ToUpper(c[0]) == char.ToUpper(letter)).ToArray();

            // Формируем клавиатуру столбиком (одна компания на строку)
            var buttons = filtered.Select(company =>
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData(company, "company:" + company)
            ).Select(btn => new[] { btn }).ToList();

            // Добавляем строку с кнопкой "Назад"
            buttons.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("Назад", "back")
            });

            return new InlineKeyboardMarkup(buttons);
        }
    }
}
