using System;
using System.Net;
using System.Net.Mail;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;

namespace SupportBot
{
    class Program
    {
        // Замените строки ниже на ваши данные
        private static readonly string TelegramToken = "8011060591:AAEMe8FQMU6umojd4PyguEppE76VyH90MAo";

        static async Task Main(string[] args)
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            var botClient = new TelegramBotClient(TelegramToken);
            var me = await botClient.GetMe(cts.Token);
            Console.WriteLine($"Бот запущен: {me.FirstName} (ID: {me.Id})");

            // Определяем, какие типы обновлений нас интересуют (только сообщения)
            ReceiverOptions receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new UpdateType[] { UpdateType.Message }
            };

            // Создаем экземпляр нашего обработчика обновлений
            var updateHandler = new MyUpdateHandler();

            // Запускаем получение обновлений
            botClient.StartReceiving(
                updateHandler: updateHandler,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот работает. Нажмите любую клавишу для завершения...");
            Console.ReadKey();
            cts.Cancel();
        }
    }
}
