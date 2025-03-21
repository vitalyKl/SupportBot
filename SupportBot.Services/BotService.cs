using System;
using System.Threading.Tasks;
using SupportBot.Core.Entities;
using SupportBot.Core.Interfaces.Data;
using SupportBot.Core.Interfaces.Services;

namespace SupportBot.Services
{
    public class BotService : IBotService
    {
        // Здесь используем конкретную сущность – Message
        private readonly ICsvRepository<Message> _csvRepository;

        public BotService(ICsvRepository<Message> csvRepository)
        {
            _csvRepository = csvRepository;
        }

        public async Task ProcessAsync()
        {
            // Пример бизнес-логики: создание и сохранение сообщения
            var message = new Message
            {
                Id = 1,
                Content = "Привет, это тестовое сообщение!",
                CreatedAt = DateTime.Now
            };

            await _csvRepository.SaveAsync(message);
        }
    }
}
