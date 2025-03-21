using System.Threading.Tasks;

namespace SupportBot.Core.Interfaces.Services
{
    public interface ITelegramBotService
    {
        Task StartAsync();
    }
}
