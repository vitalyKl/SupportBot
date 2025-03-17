using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramEmailBot.Models;

namespace TelegramEmailBot.Services
{
    public class EmailGroupingManager
    {
        private readonly EmailSender _emailSender;
        private readonly int _groupingDelaySeconds;
        private readonly CompanyBindingService _companyBindingService;
        private readonly ConcurrentDictionary<long, GroupEntry> _pendingGroups = new();
        private readonly ConcurrentDictionary<long, GroupEntry> _pendingNotBound = new();

        public EmailGroupingManager(EmailSender emailSender, int groupingDelaySeconds, CompanyBindingService companyBindingService)
        {
            _emailSender = emailSender;
            _groupingDelaySeconds = groupingDelaySeconds;
            _companyBindingService = companyBindingService;
        }

        public void AddMessage(long chatId, ForwardedMessage message, ITelegramBotClient botClient, CancellationToken outerCancellationToken)
        {
            var groupEntry = _pendingGroups.GetOrAdd(chatId, _ => new GroupEntry());
            lock (groupEntry)
            {
                groupEntry.Messages.Add(message);
                groupEntry.CancellationSource.Cancel();
                groupEntry.CancellationSource = new CancellationTokenSource();
            }
            _ = ScheduleEmailAsync(chatId, groupEntry, botClient, outerCancellationToken);
        }

        private async Task ScheduleEmailAsync(long chatId, GroupEntry groupEntry, ITelegramBotClient botClient, CancellationToken outerCancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_groupingDelaySeconds), groupEntry.CancellationSource.Token);
                if (_pendingGroups.TryRemove(chatId, out GroupEntry finishedGroup))
                {
                    if (finishedGroup.Messages.Count > 0 &&
                        finishedGroup.Messages[0].SenderId.HasValue &&
                        !_companyBindingService.TryGetCompany(finishedGroup.Messages[0].SenderId.Value, out _))
                    {
                        _pendingNotBound.TryAdd(chatId, finishedGroup);
                        return;
                    }
                    await _emailSender.SendEmailAsync(finishedGroup.Messages);
                    await botClient.SendMessage(chatId, "Группа сообщений отправлена на email.", cancellationToken: outerCancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                // Задержка отменена – группа обновилась.
            }
        }

        public async Task TryTriggerPendingEmailAsync(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            if (_pendingNotBound.TryRemove(chatId, out GroupEntry group))
            {
                await _emailSender.SendEmailAsync(group.Messages);
                await botClient.SendMessage(chatId, "Группа сообщений отправлена на email после привязки.", cancellationToken: cancellationToken);
            }
        }

        private class GroupEntry
        {
            public List<ForwardedMessage> Messages { get; } = new List<ForwardedMessage>();
            public CancellationTokenSource CancellationSource { get; set; } = new CancellationTokenSource();
        }
    }
}
