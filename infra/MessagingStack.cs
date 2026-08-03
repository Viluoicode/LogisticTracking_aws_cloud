using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace Logistics.Infra;

/// <summary>
/// B12 — SNS topic + 2 SQS queue + 2 DLQ (redrive maxReceiveCount=3) + subscription fan-out,
/// dựng bằng CDK cho AWS (trước đây chỉ có ở LocalStack local). Kèm alarm khi DLQ có message.
/// </summary>
public class MessagingStack : Stack
{
    public Topic Topic { get; }
    public Queue TrackingQueue { get; }
    public Queue NotifQueue { get; }

    public MessagingStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        Topic = new Topic(this, "ShipmentStatusTopic", new TopicProps
        {
            TopicName = "shipment-status-changed"
        });

        TrackingQueue = MakeQueueWithDlq("tracking");
        NotifQueue = MakeQueueWithDlq("notif");

        // Fan-out: mỗi queue nhận bản sao raw (body = JSON gốc).
        Topic.AddSubscription(new SqsSubscription(TrackingQueue, new SqsSubscriptionProps { RawMessageDelivery = true }));
        Topic.AddSubscription(new SqsSubscription(NotifQueue, new SqsSubscriptionProps { RawMessageDelivery = true }));

        _ = new CfnOutput(this, "TopicArn", new CfnOutputProps { Value = Topic.TopicArn });
    }

    private Queue MakeQueueWithDlq(string name)
    {
        var dlq = new Queue(this, $"{name}-dlq", new QueueProps { QueueName = $"{name}-dlq" });

        var queue = new Queue(this, $"{name}-queue", new QueueProps
        {
            QueueName = $"{name}-queue",
            DeadLetterQueue = new DeadLetterQueue { Queue = dlq, MaxReceiveCount = 3 }
        });

        // Alarm: DLQ có message = có message xử lý lỗi -> cần điều tra.
        dlq.MetricApproximateNumberOfMessagesVisible().CreateAlarm(this, $"{name}DlqNotEmptyAlarm", new CreateAlarmOptions
        {
            Threshold = 1,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            AlarmDescription = $"{name}-dlq co message loi"
        });

        return queue;
    }
}
