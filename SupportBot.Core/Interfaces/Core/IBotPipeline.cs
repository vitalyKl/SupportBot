using System.Threading.Tasks;

namespace SupportBot.Core.Interfaces.Core
{
    public interface IBotPipeline
    {
        Task ExecuteAsync();
    }
}
