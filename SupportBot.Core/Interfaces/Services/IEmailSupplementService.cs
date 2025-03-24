using System;
using SupportBot.Core.Entities;

namespace SupportBot.Core.Interfaces.Services
{
    public interface IEmailSupplementService
    {
        void SupplementEmailMessage(string senderId, string senderUserName, string senderFirstName, string messageText, DateTime messageDate);
        EmailMessage? GetEmailMessage();
        void ClearEmailMessage();
        void SetBoundCompany(string companyName);
    }
}
