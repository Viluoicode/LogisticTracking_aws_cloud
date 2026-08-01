# AWS Walkthrough — Logistics Tracking

Tài liệu học: giải thích **từng resource AWS** trong dự án làm gì, tương ứng dòng CDK nào,
và **cách tự làm lại từ số 0**. Mỗi phần theo cấu trúc:
*khái niệm → code CDK → resource trên Console → tự kiểm chứng (CLI) → tự làm lại → điểm phỏng vấn.*

Region: `ap-southeast-1` (Singapore). IaC: **AWS CDK bằng C#** (thư mục `infra/`).

---

## 0. Mental model — hành trình 1 request

Hiểu cái này là hiểu 80%. Mọi resource chỉ là một trạm trên đường đi:

```
http://<ALB-dns>/shipments/...
   │
   ▼
[1] ALB (public subnet, port 80)  ── listener rule: path "/shipments*"? → Target Group Shipment
   │                                  (không khớp → 404 "No matching route")
   ▼
[2] Target Group  → chọn 1 task Fargate "healthy" (trả 200 ở /health)
   ▼
[3] Fargate task (.NET, Kestrel :8080)
   │   • EcsSg: CHỈ ALB gọi được vào 8080
   │   • image kéo từ ECR lúc khởi động
   │   • env DB_* tiêm từ Secrets Manager
   ▼
[4] RDS PostgreSQL (private subnet)  ── RdsSg: CHỈ task ECS gọi được 5432
   ▼
   response ← ← ←   (log container → CloudWatch)
```

## Các stack & thứ tự phụ thuộc

| Stack | Chứa gì | Phụ thuộc |
|---|---|---|
| `Logistics-Network` | VPC, subnet, 3 Security Group | — |
| `Logistics-Ecr` | 3 ECR repo | — |
| `Logistics-Data` | RDS + Secrets Manager | Network (VPC, RdsSg) |
| `Logistics-Compute` | ECS, ALB, Fargate | Network + Ecr + Data |

## Yêu cầu để tự làm lại từ số 0

1. Tài khoản AWS + IAM user có quyền, chạy `aws configure` (nhập key + region `ap-southeast-1`).
2. Công cụ: .NET 9 SDK, Node.js (cho CDK CLI), `npm i -g aws-cdk`, Docker Desktop.
3. `cdk bootstrap` **một lần/account/region** — dựng resource nền để CDK deploy (S3 bucket chứa asset, IAM role). Lệnh: `cdk bootstrap aws://<account-id>/ap-southeast-1`.

---

# ① NetworkStack — mạng nền

File: [`infra/NetworkStack.cs`](../infra/NetworkStack.cs)

## Khái niệm

| Thuật ngữ | Là gì | Ví dụ đời thường |
|---|---|---|
| **VPC** | Mạng riêng ảo, có dải IP riêng bạn tự chọn | Khu đất có hàng rào |
| **CIDR** `10.0.0.0/16` | Dải IP của VPC (~65k địa chỉ) | Số nhà từ …0.0 đến …255.255 |
| **Availability Zone (AZ)** | 1 cụm data center vật lý tách biệt | 2 tòa nhà khác nhau để phòng cháy |
| **Subnet** | Chia nhỏ VPC; *public* có đường ra net, *private-isolated* thì không | Lô đất mặt tiền vs lô trong hẻm kín |
| **Internet Gateway (IGW)** | Cửa ra/vào internet cho VPC | Cổng chính khu đất |
| **Security Group (SG)** | Tường lửa quanh resource, **stateful** | Bảo vệ gác cửa từng tòa nhà |

**Stateful nghĩa là:** cho request đi vào thì response tự động được đi ra, không cần mở luật riêng.
(NACL — tường lửa quanh subnet, *stateless* — ở đây dùng mặc định, không cần đụng.)

## Code CDK (giải thích)

```csharp
Vpc = new Vpc(this, "LogisticsVpc", new VpcProps
{
    IpAddresses = IpAddresses.Cidr("10.0.0.0/16"),  // dải IP
    MaxAzs = 2,                                       // trải 2 AZ (chịu lỗi)
    NatGateways = 0,                                  // KHÔNG NAT (tiết kiệm ~$32/th)
    SubnetConfiguration = new[]
    {
        new SubnetConfiguration {                     // subnet ra net → ALB (+ Fargate)
            Name = "public",  SubnetType = SubnetType.PUBLIC,           CidrMask = 24 },
        new SubnetConfiguration {                     // subnet kín → RDS
            Name = "private", SubnetType = SubnetType.PRIVATE_ISOLATED, CidrMask = 24 }
    }
});
```

`MaxAzs=2` + 2 loại subnet → CDK tự tạo **4 subnet** (mỗi loại 1 cái/AZ). `CidrMask=24` = mỗi subnet ~256 IP.

3 Security Group dựng theo **chuỗi tin cậy** (mỗi tầng chỉ tin tầng trước):

```csharp
AlbSg.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(80),  "HTTP from internet");
AlbSg.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(443), "HTTPS from internet");
EcsSg.AddIngressRule(AlbSg,  Port.Tcp(8080), "Only ALB to container port 8080");
RdsSg.AddIngressRule(EcsSg,  Port.Tcp(5432), "Only ECS to Postgres 5432");
```

Đọc là: *internet→ALB(80/443)*, *ALB→ECS(8080)*, *ECS→RDS(5432)*. RDS bị khóa kín, internet không chạm tới.

Cuối cùng `CfnOutput` xuất `VpcId/AlbSgId/EcsSgId/RdsSgId` để stack Data/Compute import.

## Resource trên Console (tự xem)

VPC Console → `ap-southeast-1`:
- **Your VPCs**: thấy `LogisticsVpc` CIDR 10.0.0.0/16
- **Subnets**: 4 subnet (public1/2, private1/2), khác AZ
- **Route Tables**: public route có dòng `0.0.0.0/0 → igw-…`; private KHÔNG có (nên "isolated")
- **Internet Gateways**: 1 cái attach vào VPC
- **Security Groups**: 3 SG; bấm AlbSg → tab *Inbound rules* thấy 80/443 from 0.0.0.0/0

## Tự kiểm chứng bằng CLI

```powershell
aws ec2 describe-vpcs --filters "Name=cidr,Values=10.0.0.0/16" --query "Vpcs[].VpcId" --output text
```

```powershell
aws ec2 describe-subnets --filters "Name=vpc-id,Values=<vpc-id>" --query "Subnets[].{AZ:AvailabilityZone,Cidr:CidrBlock,Public:MapPublicIpOnLaunch}" --output table
```

```powershell
aws ec2 describe-nat-gateways --query "NatGateways[?State=='available']" --output text
```
(Lệnh cuối phải **rỗng** — xác nhận không có NAT.)

## Tự làm lại từ số 0

1. `mkdir infra && cd infra && dotnet new console`
2. `dotnet add package Amazon.CDK.Lib && dotnet add package Constructs`
3. Tạo `cdk.json`: `{ "app": "dotnet run --project <ten>.csproj", "context": {} }`
4. Viết `NetworkStack` như trên; trong `Program.cs`: `new NetworkStack(app, "Logistics-Network", ...)`.
5. `cdk synth` (offline, kiểm code) → `cdk deploy Logistics-Network` (cần đã `aws configure` + `cdk bootstrap`).

## Điểm phỏng vấn

- *Public vs private subnet khác nhau ở đâu?* → route table có/không đường ra IGW.
- *SG vs NACL?* → SG quanh resource, stateful; NACL quanh subnet, stateless.
- *Vì sao 2 AZ?* → 1 AZ sập vẫn chạy (reliability).
- *Vì sao chuỗi SG ALB→ECS→RDS thay vì mở cổng cho IP?* → least-privilege; DB không bao giờ lộ ra internet.
