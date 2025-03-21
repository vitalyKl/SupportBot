using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SupportBot.Core.Configurations;
using SupportBot.Core.Interfaces.Core;
using SupportBot.Core.Interfaces.Data;
using SupportBot.Core.Interfaces.Security;
using SupportBot.Core.Interfaces.Services;
using SupportBot.Infrastructure;
using SupportBot.Services;

namespace SupportBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // Регистрация настроек BotSettings из конфигурации.
                    services.Configure<BotSettings>(context.Configuration.GetSection("BotSettings"));

                    // Регистрация инфраструктурных сервисов.
                    services.AddSingleton(typeof(ICsvRepository<>), typeof(CsvRepository<>));
                    services.AddSingleton<IAesEncryptionService, AesEncryptionService>();

                    // Регистрация сервисов бизнес-логики и Telegram.
                    services.AddTransient<IBotService, BotService>();
                    services.AddSingleton<ITelegramBotService, TelegramBotService>();
                    services.AddSingleton<IBotPipeline, BotPipeline>();
                })
                .Build();

            var telegramService = host.Services.GetRequiredService<ITelegramBotService>();
            await telegramService.StartAsync();

            var pipeline = host.Services.GetRequiredService<IBotPipeline>();
            await pipeline.ExecuteAsync();

            await host.RunAsync();
        }
    }
}
