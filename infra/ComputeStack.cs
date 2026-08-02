using Amazon.CDK;
using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.RDS;
using Constructs;

namespace Logistics.Infra;

/// <summary>
/// M4 — ECS Fargate + ALB. Import VPC/SG (M1), ECR repo (M2), RDS secret (M3b).
/// Task chạy ở public subnet + assignPublicIp để né NAT (quyết định cost đã chốt);
/// EcsSg vẫn chặn mọi inbound trừ từ ALB. ALB route theo path tới 2 service.
/// </summary>
public class ComputeStack : Stack
{
    private readonly Cluster _cluster;
    private readonly ApplicationListener _listener;
    private readonly ISecurityGroup _ecsSg;
    private readonly DatabaseInstance _db;
    private int _priority = 10;

    public ComputeStack(
        Construct scope, string id,
        IVpc vpc, ISecurityGroup albSg, ISecurityGroup ecsSg,
        IRepository shipmentRepo, IRepository trackingRepo,
        DatabaseInstance db, IStackProps? props = null)
        : base(scope, id, props)
    {
        _ecsSg = ecsSg;
        _db = db;

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

        // A4: có ACM cert (truyền -c certArn=arn:aws:acm:...) -> HTTPS 443 + redirect 80->443.
        //     Không có -> HTTP 80 (dev). Encryption in transit chỉ bật khi bạn có domain + cert.
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
                DefaultAction = ListenerAction.Redirect(new RedirectOptions
                {
                    Protocol = "HTTPS",
                    Port = "443",
                    Permanent = true
                })
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

        AddHttpService("Shipment", shipmentRepo, "/shipments*");
        AddHttpService("Tracking", trackingRepo, "/track*");

        // M7: alarm khi ALB trả nhiều lỗi 5xx (dấu hiệu service hỏng).
        alb.Metrics.HttpCodeElb(HttpCodeElb.ELB_5XX_COUNT).CreateAlarm(this, "Alb5xxAlarm", new CreateAlarmOptions
        {
            Threshold = 5,
            EvaluationPeriods = 1,
            AlarmDescription = "ALB tra 5xx >= 5 trong 1 chu ky"
        });

        _ = new CfnOutput(this, "AlbDns", new CfnOutputProps { Value = alb.LoadBalancerDnsName });
    }

    private void AddHttpService(string name, IRepository repo, string pathPattern)
    {
        var taskDef = new FargateTaskDefinition(this, $"{name}TaskDef", new FargateTaskDefinitionProps
        {
            Cpu = 256,              // 0.25 vCPU — rẻ nhất
            MemoryLimitMiB = 512
        });

        var container = taskDef.AddContainer($"{name}Container", new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromEcrRepository(repo, "latest"),
            Logging = LogDrivers.AwsLogs(new AwsLogDriverProps
            {
                StreamPrefix = name.ToLowerInvariant(),
                LogRetention = RetentionDays.ONE_WEEK
            }),
            Environment = new Dictionary<string, string>
            {
                ["ASPNETCORE_HTTP_PORTS"] = "8080"
            },
            // Creds DB resolve từ Secrets Manager lúc runtime (không plaintext)
            Secrets = new Dictionary<string, Secret>
            {
                ["DB_HOST"] = Secret.FromSecretsManager(_db.Secret!, "host"),
                ["DB_PORT"] = Secret.FromSecretsManager(_db.Secret!, "port"),
                ["DB_USER"] = Secret.FromSecretsManager(_db.Secret!, "username"),
                ["DB_PASSWORD"] = Secret.FromSecretsManager(_db.Secret!, "password"),
                ["DB_NAME"] = Secret.FromSecretsManager(_db.Secret!, "dbname")
            }
        });

        container.AddPortMappings(new PortMapping { ContainerPort = 8080 });

        var service = new FargateService(this, $"{name}Service", new FargateServiceProps
        {
            Cluster = _cluster,
            TaskDefinition = taskDef,
            DesiredCount = 2,        // A5: >=2 task -> HA (trải 2 AZ), không còn SPOF
            SecurityGroups = new[] { _ecsSg },
            AssignPublicIp = true,   // public subnet -> ra internet qua IGW, né NAT
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC }
        });

        // A5: autoscaling theo CPU (2 -> 6 task).
        var scaling = service.AutoScaleTaskCount(new EnableScalingProps { MinCapacity = 2, MaxCapacity = 6 });
        scaling.ScaleOnCpuUtilization($"{name}CpuScaling", new CpuUtilizationScalingProps
        {
            TargetUtilizationPercent = 60
        });

        // M7: alarm khi CPU service cao kéo dài.
        service.MetricCpuUtilization().CreateAlarm(this, $"{name}CpuHighAlarm", new CreateAlarmOptions
        {
            Threshold = 80,
            EvaluationPeriods = 3,
            AlarmDescription = $"{name} CPU > 80%"
        });

        _priority += 10;
        _listener.AddTargets($"{name}Target", new AddApplicationTargetsProps
        {
            Priority = _priority,
            Conditions = new[] { ListenerCondition.PathPatterns(new[] { pathPattern }) },
            Port = 8080,
            Protocol = ApplicationProtocol.HTTP,
            Targets = new[] { service },
            HealthCheck = new Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck
            {
                Path = "/health",
                HealthyHttpCodes = "200"
            }
        });
    }
}
