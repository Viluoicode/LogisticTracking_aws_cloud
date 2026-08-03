using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Logistics.BuildingBlocks.Infrastructure.Resilience;
using Logistics.Shipment.Application.Abstractions;
using Polly;

namespace Logistics.Shipment.Infrastructure.Messaging;

/// <summary>Adapter: publish payload lên SNS topic, gắn attribute "type" để consumer nhận dạng.</summary>
public sealed class SnsEventPublisher(IAmazonSimpleNotificationService sns) : IEventPublisher
{
    private readonly string _topicArn = Environment.GetEnvironmentVariable("SNS_TOPIC_ARN")
        ?? throw new InvalidOperationException("SNS_TOPIC_ARN environment variable is not set.");

    // B7/B8/B10: retry+backoff, circuit breaker, timeout dùng chung.
    private static readonly ResiliencePipeline Pipeline = MessagingResilience.Build();

    public async Task PublishAsync(string messageType, string payload, string? traceParent, CancellationToken ct)
    {
        var attributes = new Dictionary<string, MessageAttributeValue>
        {
            ["type"] = new MessageAttributeValue { DataType = "String", StringValue = messageType }
        };
        // B9: truyền traceparent để consumer nối lại trace (distributed tracing xuyên service).
        if (!string.IsNullOrEmpty(traceParent))
            attributes["traceparent"] = new MessageAttributeValue { DataType = "String", StringValue = traceParent };

        var request = new PublishRequest { TopicArn = _topicArn, Message = payload, MessageAttributes = attributes };

        await Pipeline.ExecuteAsync(async token => { await sns.PublishAsync(request, token); }, ct);
    }
}
