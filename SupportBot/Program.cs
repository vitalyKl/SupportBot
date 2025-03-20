using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using TelegramEmailBot.Handlers;
using TelegramEmailBot.Handlers.Commands;
using TelegramEmailBot.Handlers.Keyboards;
using TelegramEmailBot.Models;
using TelegramEmailBot.Services;
using TelegramEmailBot.Services.Security;

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

                // Конфигурационные модели
                services.Configure<AccessOptions>(configuration.GetSection("Access"));
                services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));
                services.Configure<EmailOptions>(configuration.GetSection("Email"));
                services.Configure<TelegramEmailBot.Models.FileOptions>(configuration.GetSection("Files"));

                string telegramToken = configuration.GetSection("Telegram").GetValue<string>("Token")!;
                services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramToken));
                

                var fileOptions = configuration.GetSection("Files").Get<TelegramEmailBot.Models.FileOptions>()!;
                services.AddSingleton<CompanyBindingService>(sp => new CompanyBindingService(fileOptions.CompanyBindingsFile));
                services.AddSingleton<CompanyListService>(sp => new CompanyListService(fileOptions.CompaniesFile));

                int groupingDelaySeconds = configuration.GetValue<int>("GroupingDelaySeconds", 10);

                services.AddSingleton<EmailSender>(sp =>
                {
                    var emailOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value;
                    var bindingService = sp.GetRequiredService<CompanyBindingService>();
                    return new EmailSender(emailOptions.GmailUser, emailOptions.GmailAppPassword, emailOptions.RecipientEmail, bindingService);
                });
                services.AddSingleton<EmailPipelineProcessor>();
                services.AddSingleton<BindingEnricher>();

                // Генератор клавиатур
                services.AddSingleton<KeyboardGenerator>();

                // Обработчики команд
                services.AddSingleton<AdminCommandHandler>();
                services.AddSingleton<BindCommandHandler>();
                services.AddSingleton<UnbindCommandHandler>();

                // Регистрация нового сервиса ProgressKeyboardService
                services.AddSingleton<ProgressKeyboardService>();

                // Диспетчер команд/конвейера
                services.AddSingleton<CommandDispatcher>();

                // Основной обработчик обновлений
                services.AddSingleton<Telegram.Bot.Polling.IUpdateHandler, MyUpdateHandler>();

                // Hosted service для запуска бота
                services.AddHostedService<BotHostedService>();
            })
            .Build();

        await host.RunAsync();
    }
}
