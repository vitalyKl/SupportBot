using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SupportBot.Core.Configurations;
using SupportBot.Core.Interfaces.Services;
using SupportBot.Services;

namespace SupportBot
{
    public class Program
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
                    services.Configure<BotSettings>(context.Configuration.GetSection("BotSettings"));
                    services.Configure<EmailSettings>(context.Configuration.GetSection("EmailSettings"));

                    services.AddSingleton<ITelegramBotService, TelegramBotService>();
                    services.AddSingleton<IEmailSupplementService, EmailSupplementService>();
                    services.AddSingleton<IEmailSendingService, EmailSendingService>();
                    services.AddSingleton<IInlineKeyboardService, InlineKeyboardService>();
                    services.AddSingleton<ICompanyBindingService, CompanyBindingService>();
                    services.AddSingleton<ICompanyService, CompanyService>();
                })
                .Build();

            var telegramService = host.Services.GetRequiredService<ITelegramBotService>();
            await telegramService.StartAsync();

            await host.RunAsync();
        }
    }
}
