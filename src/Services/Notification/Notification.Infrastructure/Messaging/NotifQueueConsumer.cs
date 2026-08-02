using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Logistics.Notification.Application.Abstractions;
using Logistics.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logistics.Notification.Infrastructure.Messaging;

/// <summary>
/// Long-poll notif-queue -> "gửi" thông báo (log + ghi DB), idempotent. Message lỗi KHÔNG delete
/// -> SQS giao lại -> sau maxReceiveCount rơi vào DLQ (cấu hình redrive trong bootstrap).
/// </summary>
public sealed class NotifQueueConsumer(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    ILogger<NotifQueueConsumer> logger) : BackgroundService
{
    private string _queueUrl = "";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _queueUrl = await ResolveQueueUrlAsync(stoppingToken);
        logger.LogInformation("Notification consumer polling {QueueUrl}", _queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Notif poll error"); await Task.Delay(2000, stoppingToken); }
        }
    }

    private async Task<string> ResolveQueueUrlAsync(CancellationToken ct)
    {
        var name = Environment.GetEnvironmentVariable("NOTIF_QUEUE_NAME") ?? "notif-queue";
        while (!ct.IsCancellationRequested)
        {
            try { return (await sqs.GetQueueUrlAsync(name, ct)).QueueUrl; }
            catch { await Task.Delay(2000, ct); }
        }
        return "";
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 5
        }, ct);

        if (response.Messages is null || response.Messages.Count == 0) return;

        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<INotificationStore>();

        foreach (var message in response.Messages)
        {
            try
            {
                var evt = JsonSerializer.Deserialize<ShipmentStatusChangedIntegrationEvent>(message.Body)
                    ?? throw new InvalidOperationException("Invalid message body");

                var isNew = await store.TryRecordAsync(evt.TrackingCode, evt.Status, evt.OccurredOnUtc, message.MessageId, ct);
                if (isNew)
                    logger.LogInformation("Notification sent for {Code}: status={Status}", evt.TrackingCode, evt.Status);

                await sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message {MessageId} -> se giao lai / DLQ", message.MessageId);
            }
        }
    }
}
