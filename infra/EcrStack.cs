using Amazon.CDK;
using Amazon.CDK.AWS.ECR;
using Constructs;

namespace Logistics.Infra;

/// <summary>
/// M2 — Container registry. Mỗi service .NET một repo ECR để ECS Fargate kéo image (M4).
/// Lifecycle giữ 5 image gần nhất (tiết kiệm storage), scan-on-push (bảo mật),
/// DESTROY + EmptyOnDelete để `cdk destroy` dọn sạch (kỷ luật tear-down portfolio).
/// Tách khỏi NetworkStack vì registry không phụ thuộc mạng.
/// </summary>
public class EcrStack : Stack
{
    public Repository ShipmentRepo { get; }
    public Repository TrackingRepo { get; }
    public Repository NotificationRepo { get; }

    public EcrStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        ShipmentRepo = MakeRepo("shipment");
        TrackingRepo = MakeRepo("tracking");
        NotificationRepo = MakeRepo("notification");
    }

    private Repository MakeRepo(string service)
    {
        var repo = new Repository(this, $"{service}-repo", new RepositoryProps
        {
            RepositoryName = $"logistics/{service}",
            ImageScanOnPush = true,
            // MUTABLE cho dễ thử nghiệm tay ở M4 (push lại tag `latest`).
            // Prod nên IMMUTABLE + tag theo git SHA — nâng cấp ở M6 (CI/CD).
            ImageTagMutability = TagMutability.MUTABLE,
            RemovalPolicy = RemovalPolicy.DESTROY,
            EmptyOnDelete = true,
            LifecycleRules = new[]
            {
                new LifecycleRule
                {
                    Description = "Keep only last 5 images",
                    MaxImageCount = 5
                }
            }
        });

        // URI để `docker tag/push` và để ECS tham chiếu ở M4.
        _ = new CfnOutput(this, $"{service}RepoUri", new CfnOutputProps
        {
            Value = repo.RepositoryUri
        });

        return repo;
    }
}
