using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Logistics.Shared.Contracts;
using Logistics.Tracking.Application.Abstractions;
using Logistics.Tracking.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logistics.Tracking.Infrastructure.Messaging;

/// <summary>
/// Chạy nền: long-poll tracking-queue -> deserialize integration event -> dựng read-model
/// (idempotent) -> xóa message. Message lỗi KHÔNG xóa -> SQS giao lại -> DLQ sau N lần (M5c-2).
/// </summary>
public sealed class TrackingQueueConsumer(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    ILogger<TrackingQueueConsumer> logger) : BackgroundService
{
    private string _queueUrl = "";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _queueUrl = await ResolveQueueUrlAsync(stoppingToken);
        logger.LogInformation("Tracking consumer polling {QueueUrl}", _queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Tracking poll error"); await Task.Delay(2000, stoppingToken); }
        }
    }

    private async Task<string> ResolveQueueUrlAsync(CancellationToken ct)
    {
        var name = Environment.GetEnvironmentVariable("TRACKING_QUEUE_NAME") ?? "tracking-queue";
        while (!ct.IsCancellationRequested)
        {
            try { return (await sqs.GetQueueUrlAsync(name, ct)).QueueUrl; }
            catch { await Task.Delay(2000, ct); } // queue có thể chưa tạo (bootstrap chạy sau)
        }
        return "";
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 5 // long polling
        }, ct);

        if (response.Messages is null || response.Messages.Count == 0) return;

        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ITrackingReadStore>();

        foreach (var message in response.Messages)
        {
            try
            {
                var evt = JsonSerializer.Deserialize<ShipmentStatusChangedIntegrationEvent>(message.Body);
                if (evt is not null)
                {
                    await store.ApplyAsync(new TrackingEntry
                    {
                        Id = Guid.NewGuid(),
                        ShipmentId = evt.ShipmentId,
                        TrackingCode = evt.TrackingCode,
                        Status = evt.Status,
                        OccurredOnUtc = evt.OccurredOnUtc
                    }, message.MessageId, ct);
                }

                await sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
            }
        }
    }
}
