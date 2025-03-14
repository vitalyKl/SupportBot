using System;
using System.Net;
using System.Net.Mail;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using TelegramEmailBot.Services;
using TelegramEmailBot.Handlers;

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

            // Создаем сервисы
            var companyBindingService = new CompanyBindingService(); // Привязки сохраняются в CSV (например, "company_bindings.csv")
            var companyListService = new CompanyListService("companies.csv"); // Список компаний из отдельного CSV
            var emailSender = new EmailSender("fmonitoringbot@gmail.com", "aoru xwsk clwd lfjm", "support@fmg24.by", companyBindingService);
            // Передаем в группировщик также CompanyBindingService для проверки привязки
            var groupingManager = new EmailGroupingManager(emailSender, groupingDelaySeconds: 10, companyBindingService);
            var messageProcessor = new TelegramMessageProcessor(TelegramToken);

            // Передаем зависимости в обработчик обновлений
            var updateHandler = new MyUpdateHandler(messageProcessor, groupingManager, companyBindingService, companyListService);

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new UpdateType[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            botClient.StartReceiving(updateHandler, receiverOptions, cts.Token);

            Console.WriteLine("Бот работает. Нажмите любую клавишу для завершения...");
            Console.ReadKey();
            cts.Cancel();
        }
    }
}
