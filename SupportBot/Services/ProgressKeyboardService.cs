using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramEmailBot.Services
{
    public class ProgressKeyboardService
    {
        public async Task SimulateProgressAsync(ITelegramBotClient botClient, long chatId, int messageId, CancellationToken cancellationToken)
        {
            int totalTime = 5000; // Общее время симуляции, например, 5 секунд
            int interval = 500;   // Интервал обновления, 500 мс
            int steps = totalTime / interval;

            for (int i = 0; i < steps; i++)
            {
                int progress = (i + 1) * 100 / steps;
                InlineKeyboardMarkup keyboard = GenerateProgressKeyboard(progress);
                await botClient.EditMessageReplyMarkup(chatId, messageId, keyboard, null, cancellationToken);
                await Task.Delay(interval, cancellationToken);
            }
        }

        private InlineKeyboardMarkup GenerateProgressKeyboard(int progressPercent)
        {
            string progressText = $"Processing: {progressPercent}%";
            var buttons = new[]
            {
                new [] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData(progressText, "progress") },
                new [] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("Cancel", "cancel") }
            };
            return new InlineKeyboardMarkup(buttons);
        }
    }
}
