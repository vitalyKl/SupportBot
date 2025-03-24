using System;
using SupportBot.Core.Entities;
using SupportBot.Core.Interfaces.Services;

namespace SupportBot.Services
{
    public class EmailSupplementService : IEmailSupplementService
    {
        private EmailMessage? _emailMessage;

        public EmailSupplementService() => _emailMessage = null;

        public void SupplementEmailMessage(string senderId, string senderUserName, string senderFirstName, string messageText, DateTime messageDate)
        {
            if (_emailMessage is null)
            {
                _emailMessage = new EmailMessage
                {
                    SenderId = senderId,
                    SenderUserName = senderUserName,
                    SenderFirstName = senderFirstName,
                    FirstMessageDate = messageDate,
                    BoundCompany = string.Empty
                };
            }
            _emailMessage.MessageTexts.Add(messageText);
        }

        public EmailMessage? GetEmailMessage() => _emailMessage;

        public void ClearEmailMessage() => _emailMessage = null;

        public void SetBoundCompany(string companyName)
        {
            if (_emailMessage is not null)
                _emailMessage.BoundCompany = companyName;
        }
    }
}
