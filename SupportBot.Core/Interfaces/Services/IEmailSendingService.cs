using System.Threading.Tasks;
using SupportBot.Core.Entities;

namespace SupportBot.Core.Interfaces.Services
{
    public interface IEmailSendingService
    {
        Task SendEmailAsync(EmailMessage emailMessage);
    }
}
