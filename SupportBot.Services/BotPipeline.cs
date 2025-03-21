using System.Threading.Tasks;
using SupportBot.Core.Interfaces.Core;
using SupportBot.Core.Interfaces.Services;

namespace SupportBot.Services
{
    public class BotPipeline : IBotPipeline
    {
        private readonly IBotService _botService;

        public BotPipeline(IBotService botService)
        {
            _botService = botService;
        }

        public async Task ExecuteAsync()
        {
            // Запуск бизнес-логики через вызов конкретного сервиса.
            await _botService.ProcessAsync();
        }
    }
}
