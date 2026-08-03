using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.RDS;
using Constructs;

namespace Logistics.Infra;

/// <summary>
/// M3b — RDS PostgreSQL cho tầng data.
/// Đặt trong private-isolated subnet + gắn RdsSg (đều import từ NetworkStack).
/// Creds sinh ngẫu nhiên vào Secrets Manager (app đọc lúc runtime ở M4).
/// Cấu hình free-tier/dev: t3.micro, single-AZ, 20GB, backup off, destroy sạch.
/// </summary>
public class DataStack : Stack
{
    public DatabaseInstance Database { get; }

    public DataStack(Construct scope, string id, IVpc vpc, ISecurityGroup rdsSg, bool multiAz = false, IStackProps? props = null)
        : base(scope, id, props)
    {
        Database = new DatabaseInstance(this, "ShipmentDb", new DatabaseInstanceProps
        {
            Engine = DatabaseInstanceEngine.Postgres(new PostgresInstanceEngineProps
            {
                Version = PostgresEngineVersion.Of("16.9", "16")
            }),
            InstanceType = Amazon.CDK.AWS.EC2.InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.MICRO), // db.t3.micro
            Vpc = vpc,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = new[] { rdsSg },

            // Master creds -> Secrets Manager (không bao giờ thấy plaintext)
            Credentials = Credentials.FromGeneratedSecret("logi"),
            DatabaseName = "logistics",

            AllocatedStorage = 20,        // free-tier 20GB
            StorageType = StorageType.GP2,
            StorageEncrypted = true,      // free + best practice
            MultiAz = multiAz,           // B15: prod -> Multi-AZ (HA); dev/staging -> single-AZ (rẻ)
            PubliclyAccessible = false,   // private, chỉ ECS gọi được

            // Dev/portfolio: destroy sạch, không giữ backup
            BackupRetention = Duration.Days(0),
            DeleteAutomatedBackups = true,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // Xuất cho M4 (ECS) dùng
        _ = new CfnOutput(this, "DbEndpoint", new CfnOutputProps
        {
            Value = Database.DbInstanceEndpointAddress
        });
        _ = new CfnOutput(this, "DbSecretArn", new CfnOutputProps
        {
            Value = Database.Secret!.SecretArn
        });
    }
}
