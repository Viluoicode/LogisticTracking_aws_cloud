using Amazon.CDK;

namespace Logistics.Infra;

sealed class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        // M1: mạng nền. Env lấy từ `aws configure` lúc deploy; synth không cần.
        var network = new NetworkStack(app, "Logistics-Network", new StackProps
        {
            Description = "VPC + subnets + security groups cho logistics-tracking (M1)"
        });

        // M2: container registry (độc lập với mạng).
        var ecr = new EcrStack(app, "Logistics-Ecr", new StackProps
        {
            Description = "ECR repositories cho 3 service (M2)"
        });

        // M3b: RDS Postgres — import VPC + RdsSg từ NetworkStack (cross-stack).
        var data = new DataStack(app, "Logistics-Data", network.Vpc, network.RdsSg, new StackProps
        {
            Description = "RDS PostgreSQL + Secrets Manager creds (M3b)"
        });

        // M4: ECS Fargate + ALB — ráp VPC/SG (M1) + ECR (M2) + RDS secret (M3b).
        new ComputeStack(app, "Logistics-Compute",
            network.Vpc, network.AlbSg, network.EcsSg,
            ecr.ShipmentRepo, ecr.TrackingRepo,
            data.Database,
            new StackProps { Description = "ECS Fargate services + ALB routing (M4)" });

        app.Synth();
    }
}
