using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace Logistics.Infra;

/// <summary>
/// M6 — Role cho GitHub Actions deploy qua OIDC (không dùng long-lived key).
/// Trust: token từ GitHub OIDC + ĐÚNG repo. Quyền: push ECR + update ECS.
/// Deploy: cdk deploy Logistics-Cicd -c githubRepo=owner/repo
/// </summary>
public class CicdStack : Stack
{
    public CicdStack(Construct scope, string id, string githubRepo, IStackProps? props = null)
        : base(scope, id, props)
    {
        // OIDC provider cho GitHub Actions (mỗi account chỉ cần 1).
        var provider = new OpenIdConnectProvider(this, "GithubOidc", new OpenIdConnectProviderProps
        {
            Url = "https://token.actions.githubusercontent.com",
            ClientIds = new[] { "sts.amazonaws.com" }
        });

        // Role chỉ cho phép repo này assume (điều kiện sub = repo:owner/repo:*).
        var role = new Role(this, "GithubActionsDeployRole", new RoleProps
        {
            RoleName = "github-actions-deploy",
            AssumedBy = new WebIdentityPrincipal(provider.OpenIdConnectProviderArn, new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, object>
                {
                    ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com"
                },
                ["StringLike"] = new Dictionary<string, object>
                {
                    ["token.actions.githubusercontent.com:sub"] = $"repo:{githubRepo}:*"
                }
            })
        });

        // Quyền tối thiểu cho pipeline: push ECR + roll ECS.
        role.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AmazonEC2ContainerRegistryPowerUser"));
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "ecs:UpdateService", "ecs:DescribeServices", "ecs:ListServices", "ecs:ListClusters" },
            Resources = new[] { "*" }
        }));

        _ = new CfnOutput(this, "DeployRoleArn", new CfnOutputProps { Value = role.RoleArn });
    }
}
