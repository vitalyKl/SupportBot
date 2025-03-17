using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramEmailBot.Models;
using TelegramEmailBot.Services;
using Xunit;
using Moq;
using Moq.Protected;

namespace SupportBot.Tests
{
    public class TelegramMessageProcessorTests
    {
        [Fact]
        public async Task ProcessMessageAsync_ShouldReturnForwardedMessage_WithCorrectText()
        {
            // Arrange
            string fakeToken = "fake-token";
            var processor = new TelegramMessageProcessor(fakeToken);

            // Создаем минимальное сообщение для теста
            var testMessage = new Message
            {
                MessageId = 123,
                Date = DateTime.Now,
                Text = "Test message",
                ForwardFrom = new User { Id = 456, FirstName = "Test", LastName = "User", Username = "testuser" }
            };

            // Создаем мок TelegramBotClient, который вернет пустой файл при вызове GetFileAsync
            var mockBotClient = new Mock<ITelegramBotClient>();
            // Можно добавить Setup, если метод GetFileAsync вызывается, но для базового теста текст не зависит от скачивания файла.
            // ...

            // Act
            ForwardedMessage result = await processor.ProcessMessageAsync(mockBotClient.Object, testMessage, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test message", result.Text);
            Assert.Contains("Test", result.SenderInfo);
            Assert.Equal(456, result.SenderId);
        }
    }
}
