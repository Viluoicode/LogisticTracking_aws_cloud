using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Constructs;

namespace Logistics.Infra;

/// <summary>
/// M1 — Mạng nền cho logistics-tracking.
/// VPC 2 AZ, public subnet (cho ALB) + private-isolated subnet (cho ECS/RDS),
/// natGateways=0 (quyết định NAT vs Endpoint vs public-Fargate để dành cho M4).
/// 3 Security Group theo chuỗi tin cậy: ALB -> ECS -> RDS.
/// </summary>
public class NetworkStack : Stack
{
    public Vpc Vpc { get; }
    public SecurityGroup AlbSg { get; }
    public SecurityGroup EcsSg { get; }
    public SecurityGroup RdsSg { get; }

    public NetworkStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // --- VPC: 2 AZ, không NAT ---
        Vpc = new Vpc(this, "LogisticsVpc", new VpcProps
        {
            IpAddresses = IpAddresses.Cidr("10.0.0.0/16"),
            MaxAzs = 2,
            NatGateways = 0,
            SubnetConfiguration = new[]
            {
                new SubnetConfiguration
                {
                    Name = "public",                       // cho ALB
                    SubnetType = SubnetType.PUBLIC,
                    CidrMask = 24
                },
                new SubnetConfiguration
                {
                    Name = "private",                      // cho ECS + RDS, không ra internet
                    SubnetType = SubnetType.PRIVATE_ISOLATED,
                    CidrMask = 24
                }
            }
        });

        // --- Security Groups: chuỗi tin cậy ALB -> ECS -> RDS ---

        // 1) ALB: nhận HTTP/HTTPS từ internet
        AlbSg = new SecurityGroup(this, "AlbSg", new SecurityGroupProps
        {
            Vpc = Vpc,
            Description = "ALB - accept HTTP/HTTPS from internet",
            AllowAllOutbound = true
        });
        AlbSg.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(80), "HTTP from internet");
        AlbSg.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(443), "HTTPS from internet");

        // 2) ECS: CHỈ nhận từ ALB (không phải IP nào khác)
        EcsSg = new SecurityGroup(this, "EcsSg", new SecurityGroupProps
        {
            Vpc = Vpc,
            Description = "ECS Fargate - accept only from ALB",
            AllowAllOutbound = true
        });
        EcsSg.AddIngressRule(AlbSg, Port.Tcp(8080), "Only ALB to container port 8080");

        // 3) RDS: CHỈ nhận từ ECS
        RdsSg = new SecurityGroup(this, "RdsSg", new SecurityGroupProps
        {
            Vpc = Vpc,
            Description = "RDS Postgres - accept only from ECS",
            AllowAllOutbound = true
        });
        RdsSg.AddIngressRule(EcsSg, Port.Tcp(5432), "Only ECS to Postgres 5432");

        // --- Outputs cho các stack sau (ECS/RDS ở M3/M4) tham chiếu ---
        _ = new CfnOutput(this, "VpcId", new CfnOutputProps { Value = Vpc.VpcId });
        _ = new CfnOutput(this, "AlbSgId", new CfnOutputProps { Value = AlbSg.SecurityGroupId });
        _ = new CfnOutput(this, "EcsSgId", new CfnOutputProps { Value = EcsSg.SecurityGroupId });
        _ = new CfnOutput(this, "RdsSgId", new CfnOutputProps { Value = RdsSg.SecurityGroupId });
    }
}
