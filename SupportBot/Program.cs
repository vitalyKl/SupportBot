using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using TelegramEmailBot.Handlers;
using TelegramEmailBot.Models;
using TelegramEmailBot.Services;

namespace TelegramEmailBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // Регистрируем опции (обязательно заполненные в appsettings.json)
                    services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));
                    services.Configure<EmailOptions>(configuration.GetSection("Email"));
                    services.Configure<Models.FileOptions>(configuration.GetSection("Files"));

                    // Извлекаем токен с операцией null-forgiving:
                    string telegramToken = configuration.GetSection("Telegram").GetValue<string>("Token")!;
                    services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramToken));

                    // Регистрация сервисов. Для сервисов, принимающих строки, используем фабрики:
                    services.AddSingleton<TelegramMessageProcessor>(sp =>
                    {
                        // Используем тот же токен, что и выше
                        return new TelegramMessageProcessor(telegramToken);
                    });

                    // Для EmailSender тоже используем фабрику:
                    services.AddSingleton<EmailSender>(sp =>
                    {
                        var emailOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value;
                        var bindingService = sp.GetRequiredService<CompanyBindingService>();
                        // Если какие-либо настройки отсутствуют, можно бросить исключение:
                        if (string.IsNullOrWhiteSpace(emailOptions.GmailUser) ||
                            string.IsNullOrWhiteSpace(emailOptions.GmailAppPassword) ||
                            string.IsNullOrWhiteSpace(emailOptions.RecipientEmail))
                        {
                            throw new Exception("Проверьте настройки Email в appsettings.json.");
                        }
                        return new EmailSender(emailOptions.GmailUser, emailOptions.GmailAppPassword, emailOptions.RecipientEmail, bindingService);
                    });

                    int groupingDelaySeconds = configuration.GetValue<int>("GroupingDelaySeconds", 10);

                    // Регистрируем другие сервисы. Для CompanyBindingService и CompanyListService передаём пути из FileOptions:
                    var fileOptions = configuration.GetSection("Files").Get<Models.FileOptions>()!;
                    services.AddSingleton<CompanyBindingService>(sp => new CompanyBindingService(fileOptions.CompanyBindingsFile));
                    services.AddSingleton<CompanyListService>(sp => new CompanyListService(fileOptions.CompaniesFile));

                    services.AddSingleton<EmailGroupingManager>(sp =>
                    {
                        var emailSender = sp.GetRequiredService<EmailSender>();
                        var bindingService = sp.GetRequiredService<CompanyBindingService>();
                        return new EmailGroupingManager(emailSender, groupingDelaySeconds, bindingService);
                    });

                    services.AddSingleton<TelegramMessageAdapter>(); // если понадобится
                    services.AddSingleton<MyUpdateHandler>();
                    services.AddHostedService<BotHostedService>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
