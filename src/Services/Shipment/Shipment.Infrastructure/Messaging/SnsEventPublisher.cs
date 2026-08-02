using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Logistics.Shipment.Application.Abstractions;

namespace Logistics.Shipment.Infrastructure.Messaging;

/// <summary>Adapter: publish payload lên SNS topic, gắn attribute "type" để consumer nhận dạng.</summary>
public sealed class SnsEventPublisher(IAmazonSimpleNotificationService sns) : IEventPublisher
{
    private readonly string _topicArn = Environment.GetEnvironmentVariable("SNS_TOPIC_ARN")
        ?? throw new InvalidOperationException("SNS_TOPIC_ARN environment variable is not set.");

    public async Task PublishAsync(string messageType, string payload, CancellationToken ct)
    {
        var request = new PublishRequest
        {
            TopicArn = _topicArn,
            Message = payload,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["type"] = new MessageAttributeValue { DataType = "String", StringValue = messageType }
            }
        };

        await sns.PublishAsync(request, ct);
    }
}
