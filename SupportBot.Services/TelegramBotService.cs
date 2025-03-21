using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using SupportBot.Core.Configurations;
using SupportBot.Core.Interfaces.Services;

namespace SupportBot.Services
{
    public class TelegramBotService : ITelegramBotService
    {
        private readonly TelegramBotClient _botClient;

        public TelegramBotService(IOptions<BotSettings> options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var botToken = options.Value.BotToken;
            if (string.IsNullOrWhiteSpace(botToken))
                throw new InvalidOperationException("Bot token is not configured.");

            _botClient = new TelegramBotClient(botToken);
        }

        public async Task StartAsync()
        {
            _botClient.StartReceiving(
                updateHandler: async (bot, update, cancellationToken) =>
                {
                    Console.WriteLine($"Получено обновление: {update.Type}");
                    await Task.CompletedTask;
                },
                errorHandler: async (bot, exception, cancellationToken) =>
                {
                    Console.WriteLine($"Ошибка: {exception.Message}");
                    await Task.CompletedTask;
                }
            );
            Console.WriteLine("Telegram Bot Service запущен.");
            await Task.CompletedTask;
        }
    }
}
