using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramEmailBot.Models;
using TelegramEmailBot.Services;
using SupportBot.Tests;
using Xunit;
using Moq;
using Telegram.Bot.Types;

namespace SupportBot.Tests
{
    public class TelegramMessageProcessorTests
    {
        [Fact]
        public async Task ProcessMessageAsync_Returns_ForwardedMessage_With_Correct_Data()
        {
            // Arrange
            var fakeToken = "fake-token";
            var processor = new TelegramMessageProcessor(fakeToken);

            var fakeMessage = new FakeIncomingMessage
            {
                MessageId = 123,
                Date = DateTime.Now,
                Text = "Test message",
                Caption = "Test caption",
                Contact = null,
                Photo = Array.Empty<Telegram.Bot.Types.PhotoSize>(),
                Document = null,
                ForwardFrom = new User
                {
                    Id = 456,
                    FirstName = "Test",
                    LastName = "User",
                    Username = "testuser"
                },
                ForwardSenderName = "ForwardedTestUser"
            };

            var fakeBotClient = new Mock<ITelegramBotClient>();

            // Act
            ForwardedMessage forwarded = await processor.ProcessMessageAsync(fakeBotClient.Object, fakeMessage, CancellationToken.None);

            // Assert
            Assert.NotNull(forwarded);
            Assert.Equal(123, forwarded.MessageId);
            Assert.Equal("Test message", forwarded.Text);
            Assert.Contains("Test", forwarded.SenderInfo);
            Assert.Equal(456, forwarded.SenderId);
        }
    }
}
