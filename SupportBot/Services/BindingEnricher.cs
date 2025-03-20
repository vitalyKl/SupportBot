using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramEmailBot.Models;
using TelegramEmailBot.Services;

namespace TelegramEmailBot.Services
{
    public class BindingEnricher
    {
        private readonly CompanyBindingService _companyBindingService;
        public BindingEnricher(CompanyBindingService companyBindingService)
        {
            _companyBindingService = companyBindingService;
        }

        public bool IsSenderBound(long senderId)
        {
            return _companyBindingService.TryGetCompany(senderId, out string binding) && !string.IsNullOrWhiteSpace(binding);
        }

        public void EnrichEmailBinding(EmailMessage email)
        {
            if (_companyBindingService.TryGetCompany(email.SenderId, out string binding) && !string.IsNullOrWhiteSpace(binding))
            {
                // Обновляем тему письма
                email.Subject = binding;
            }
        }
    }
}
