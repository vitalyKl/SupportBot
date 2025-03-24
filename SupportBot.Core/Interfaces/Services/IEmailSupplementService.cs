using System;
using SupportBot.Core.Entities;

namespace SupportBot.Core.Interfaces.Services
{
    public interface IEmailSupplementService
    {
        // Для текстовых сообщений
        void SupplementEmailMessage(string senderId, string senderUserName, string senderFirstName, string messageText, DateTime messageDate);
        // Для сообщений с вложениями (например, изображениями)
        void SupplementEmailMessageBlock(EmailMessageBlock block, string senderId, string senderUserName, string senderFirstName, DateTime messageDate);
        EmailMessage? GetEmailMessage();
        void ClearEmailMessage();
        void SetBoundCompany(string companyName);
    }
}
