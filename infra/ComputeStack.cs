using Amazon.CDK;
using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace Logistics.Infra;

/// <summary>
/// ECS Fargate + ALB. Import VPC/SG (M1), ECR (M2), RDS secret (M3b), SNS/SQS (B12).
/// Shipment/Tracking = HTTP service sau ALB; Notification = worker (không ALB).
/// IAM grant tối thiểu: Shipment publish SNS, Tracking/Notification consume SQS.
/// Image tag lấy từ -c imageTag=&lt;git-sha&gt; (B14, mặc định latest) để rollback được.
/// </summary>
public class ComputeStack : Stack
{
    private readonly Cluster _cluster;
    private readonly ApplicationListener _listener;
    private readonly ISecurityGroup _ecsSg;
    private readonly DatabaseInstance _db;
    private readonly string _imageTag;
    private int _priority = 10;

    public ComputeStack(
        Construct scope, string id,
        IVpc vpc, ISecurityGroup albSg, ISecurityGroup ecsSg,
        IRepository shipmentRepo, IRepository trackingRepo, IRepository notificationRepo,
        DatabaseInstance db, ITopic topic, IQueue trackingQueue, IQueue notifQueue,
        IStackProps? props = null)
        : base(scope, id, props)
    {
        _ecsSg = ecsSg;
        _db = db;
        _imageTag = Node.TryGetContext("imageTag") as string ?? "latest"; // B14: rollback theo SHA

        _cluster = new Cluster(this, "LogisticsCluster", new ClusterProps { Vpc = vpc });

        var alb = new ApplicationLoadBalancer(this, "Alb", new ApplicationLoadBalancerProps
        {
            Vpc = vpc,
            InternetFacing = true,
            SecurityGroup = albSg,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC }
        });

        var notFound = ListenerAction.FixedResponse(404, new FixedResponseOptions
        {
            ContentType = "text/plain",
            MessageBody = "No matching route"
        });

        // A4: có ACM cert (-c certArn=...) -> HTTPS 443 + redirect 80->443; không có -> HTTP 80 (dev).
        var certArn = Node.TryGetContext("certArn") as string;
        if (!string.IsNullOrWhiteSpace(certArn))
        {
            _listener = alb.AddListener("HttpsListener", new BaseApplicationListenerProps
            {
                Port = 443,
                Protocol = ApplicationProtocol.HTTPS,
                Certificates = new[] { ListenerCertificate.FromArn(certArn) },
                DefaultAction = notFound
            });
            alb.AddListener("HttpRedirect", new BaseApplicationListenerProps
            {
                Port = 80,
                Protocol = ApplicationProtocol.HTTP,
                DefaultAction = ListenerAction.Redirect(new RedirectOptions { Protocol = "HTTPS", Port = "443", Permanent = true })
            });
        }
        else
        {
            _listener = alb.AddListener("HttpListener", new BaseApplicationListenerProps
            {
                Port = 80,
                Protocol = ApplicationProtocol.HTTP,
                DefaultAction = notFound
            });
        }

        AddHttpService("Shipment", shipmentRepo, "/shipments*", publishTopic: topic, consumeQueue: null);
        AddHttpService("Tracking", trackingRepo, "/track*", publishTopic: null, consumeQueue: trackingQueue);
        AddWorkerService("Notification", notificationRepo, notifQueue);

        // M7: alarm khi ALB trả nhiều lỗi 5xx.
        alb.Metrics.HttpCodeElb(HttpCodeElb.ELB_5XX_COUNT).CreateAlarm(this, "Alb5xxAlarm", new CreateAlarmOptions
        {
            Threshold = 5,
            EvaluationPeriods = 1,
            AlarmDescription = "ALB tra 5xx >= 5 trong 1 chu ky"
        });

        _ = new CfnOutput(this, "AlbDns", new CfnOutputProps { Value = alb.LoadBalancerDnsName });
    }

    // DB creds resolve từ Secrets Manager lúc runtime (không plaintext).
    private Dictionary<string, Secret> DbSecrets() => new()
    {
        ["DB_HOST"] = Secret.FromSecretsManager(_db.Secret!, "host"),
        ["DB_PORT"] = Secret.FromSecretsManager(_db.Secret!, "port"),
        ["DB_USER"] = Secret.FromSecretsManager(_db.Secret!, "username"),
        ["DB_PASSWORD"] = Secret.FromSecretsManager(_db.Secret!, "password"),
        ["DB_NAME"] = Secret.FromSecretsManager(_db.Secret!, "dbname")
    };

    private void AddHttpService(string name, IRepository repo, string pathPattern, ITopic? publishTopic, IQueue? consumeQueue)
    {
        var taskDef = new FargateTaskDefinition(this, $"{name}TaskDef", new FargateTaskDefinitionProps
        {
            Cpu = 256,
            MemoryLimitMiB = 512
        });

        var env = new Dictionary<string, string> { ["ASPNETCORE_HTTP_PORTS"] = "8080" };
        if (publishTopic != null) env["SNS_TOPIC_ARN"] = publishTopic.TopicArn; // Shipment cần để publish

        var container = taskDef.AddContainer($"{name}Container", new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromEcrRepository(repo, _imageTag),
            Logging = LogDrivers.AwsLogs(new AwsLogDriverProps { StreamPrefix = name.ToLowerInvariant(), LogRetention = RetentionDays.ONE_WEEK }),
            Environment = env,
            Secrets = DbSecrets()
        });
        container.AddPortMappings(new PortMapping { ContainerPort = 8080 });

        var service = new FargateService(this, $"{name}Service", new FargateServiceProps
        {
            Cluster = _cluster,
            TaskDefinition = taskDef,
            DesiredCount = 2,        // A5: HA
            SecurityGroups = new[] { _ecsSg },
            AssignPublicIp = true,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC }
        });

        // IAM least-privilege: chỉ cấp đúng quyền cần.
        publishTopic?.GrantPublish(taskDef.TaskRole);
        consumeQueue?.GrantConsumeMessages(taskDef.TaskRole);

        var scaling = service.AutoScaleTaskCount(new EnableScalingProps { MinCapacity = 2, MaxCapacity = 6 });
        scaling.ScaleOnCpuUtilization($"{name}CpuScaling", new CpuUtilizationScalingProps { TargetUtilizationPercent = 60 });

        service.MetricCpuUtilization().CreateAlarm(this, $"{name}CpuHighAlarm", new CreateAlarmOptions
        {
            Threshold = 80, EvaluationPeriods = 3, AlarmDescription = $"{name} CPU > 80%"
        });

        _priority += 10;
        _listener.AddTargets($"{name}Target", new AddApplicationTargetsProps
        {
            Priority = _priority,
            Conditions = new[] { ListenerCondition.PathPatterns(new[] { pathPattern }) },
            Port = 8080,
            Protocol = ApplicationProtocol.HTTP,
            Targets = new[] { service },
            HealthCheck = new Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck { Path = "/health", HealthyHttpCodes = "200" }
        });
    }

    // B13: Notification worker chạy trên ECS (không ALB, chỉ consume SQS).
    private void AddWorkerService(string name, IRepository repo, IQueue consumeQueue)
    {
        var taskDef = new FargateTaskDefinition(this, $"{name}TaskDef", new FargateTaskDefinitionProps
        {
            Cpu = 256,
            MemoryLimitMiB = 512
        });

        taskDef.AddContainer($"{name}Container", new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromEcrRepository(repo, _imageTag),
            Logging = LogDrivers.AwsLogs(new AwsLogDriverProps { StreamPrefix = name.ToLowerInvariant(), LogRetention = RetentionDays.ONE_WEEK }),
            Secrets = DbSecrets()
        });

        var service = new FargateService(this, $"{name}Service", new FargateServiceProps
        {
            Cluster = _cluster,
            TaskDefinition = taskDef,
            DesiredCount = 1,        // worker: 1 đủ (SQS redelivery lo khi task chết); tăng nếu cần throughput
            SecurityGroups = new[] { _ecsSg },
            AssignPublicIp = true,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC }
        });

        consumeQueue.GrantConsumeMessages(taskDef.TaskRole);
    }
}
