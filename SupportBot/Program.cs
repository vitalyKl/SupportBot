using System;
using System.Net;
using System.Net.Mail;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using TelegramEmailBot.Services;
using TelegramEmailBot.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportBot.Models;

namespace SupportBot
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            // Создание хоста
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // Регистрируем опции, если захотите их далее использовать
                    services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));
                    services.Configure<EmailOptions>(configuration.GetSection("Email"));
                    services.Configure<Models.FileOptions>(configuration.GetSection("Files"));

                    int groupingDelaySeconds = configuration.GetValue<int>("GroupingDelaySeconds", 10);

                    // Регистрируем TelegramBotClient
                    string telegramToken = configuration.GetSection("Telegram").GetValue<string>("Token");
                    services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramToken));

                    // Регистрируем наши сервисы
                    services.AddSingleton<CompanyBindingService>();
                    services.AddSingleton<CompanyListService>();
                    services.AddSingleton<TelegramMessageProcessor>();
                    services.AddSingleton<EmailSender>();
                    services.AddSingleton<EmailGroupingManager>(sp =>
                    {
                        var emailSender = sp.GetRequiredService<EmailSender>();
                        var companyBindingService = sp.GetRequiredService<CompanyBindingService>();
                        return new EmailGroupingManager(emailSender, groupingDelaySeconds, companyBindingService);
                    });
                    services.AddSingleton<MyUpdateHandler>();
                    services.AddHostedService<BotHostedService>();  // Наш Hosted Service для бота
                })
                .Build();

            await host.RunAsync();
        }
    }
}
