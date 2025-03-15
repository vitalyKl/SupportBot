using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramEmailBot.Handlers;

namespace TelegramEmailBot.Services
{
    public class BotHostedService : IHostedService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly MyUpdateHandler _updateHandler;
        private CancellationTokenSource _cts;

        public BotHostedService(ITelegramBotClient botClient, MyUpdateHandler updateHandler)
        {
            _botClient = botClient;
            _updateHandler = updateHandler;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var receiverOptions = new ReceiverOptions { AllowedUpdates = new UpdateType[] { UpdateType.Message, UpdateType.CallbackQuery } };
            _botClient.StartReceiving(_updateHandler, receiverOptions, _cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            return Task.CompletedTask;
        }
    }
}
