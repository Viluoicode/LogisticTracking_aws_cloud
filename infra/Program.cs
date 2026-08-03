using Amazon.CDK;

namespace Logistics.Infra;

sealed class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        // B15: môi trường (dev/staging/prod). dev GIỮ tên gốc "Logistics-*" (không phá stack đã deploy);
        // staging/prod -> tiền tố riêng để deploy song song, tách biệt.  Truyền: -c env=prod
        var env = (app.Node.TryGetContext("env") as string ?? "dev").ToLowerInvariant();
        string Id(string name) => env == "dev" ? $"Logistics-{name}" : $"Logistics-{env}-{name}";
        var isProd = env == "prod";

        // M1: mạng nền.
        var network = new NetworkStack(app, Id("Network"), new StackProps
        {
            Description = "VPC + subnets + security groups (M1)"
        });

        // M2: container registry.
        var ecr = new EcrStack(app, Id("Ecr"), new StackProps
        {
            Description = "ECR repositories cho 3 service (M2)"
        });

        // B12: SNS/SQS/DLQ messaging.
        var messaging = new MessagingStack(app, Id("Messaging"), new StackProps
        {
            Description = "SNS topic + SQS queues + DLQ (B12)"
        });

        // M3b: RDS Postgres (prod -> Multi-AZ).
        var data = new DataStack(app, Id("Data"), network.Vpc, network.RdsSg, isProd, new StackProps
        {
            Description = "RDS PostgreSQL + Secrets Manager (M3b)"
        });

        // M4/B13: ECS Fargate (Shipment/Tracking API + Notification worker) + ALB.
        new ComputeStack(app, Id("Compute"),
            network.Vpc, network.AlbSg, network.EcsSg,
            ecr.ShipmentRepo, ecr.TrackingRepo, ecr.NotificationRepo,
            data.Database, messaging.Topic, messaging.TrackingQueue, messaging.NotifQueue,
            new StackProps { Description = "ECS Fargate services + ALB (M4/B13)" });

        // M6: role cho GitHub Actions deploy qua OIDC. Truyền repo: -c githubRepo=owner/repo
        var githubRepo = (app.Node.TryGetContext("githubRepo") as string) ?? "Viluoicode/logistics-tracking";
        new CicdStack(app, Id("Cicd"), githubRepo, new StackProps
        {
            Description = "GitHub Actions OIDC deploy role (M6)"
        });

        app.Synth();
    }
}
