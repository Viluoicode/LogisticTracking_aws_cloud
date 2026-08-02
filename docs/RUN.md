# Hướng dẫn chạy & hiểu dự án — Logistics Tracking

Tài liệu này giúp bạn (1) chạy toàn hệ ở **local**, (2) deploy lên **AWS**, (3) hiểu **từng mảnh đang làm gì**.

---

## 1. Kiến trúc tóm tắt

3 microservice .NET, mỗi service Clean Architecture + **DB riêng**, giao tiếp **bất đồng bộ qua event**:

```
Client
  │ HTTP
  ▼
Shipment API  ──(1) ghi shipment + outbox (atomic)──►  DB: logistics
  │ (2) OutboxDispatcher (nền) đọc outbox → publish
  ▼
SNS topic: shipment-status-changed
  │ fan-out (bản sao cho mỗi consumer)
  ├─► SQS tracking-queue ─► Tracking API (consumer) ─► DB: tracking (read-model) ─► GET /track/{code}
  └─► SQS notif-queue    ─► Notification Worker      ─► DB: notification (log)
           └─ message lỗi ×3 → notif-dlq (dead-letter queue)
```

| Service | Vai trò | Cổng (local) | DB |
|---|---|---|---|
| **Shipment.Api** | Ghi: tạo/đổi trạng thái shipment (nguồn sự thật) | 5080 | logistics |
| **Tracking.Api** | Đọc: dựng read-model từ event, tra cứu timeline | 5081 | tracking |
| **Notification.Worker** | Nghe event → "gửi" thông báo (log) | (không HTTP) | notification |

---

## 2. Yêu cầu công cụ

- **.NET 9 SDK**, **Docker Desktop**, **Node.js** + `npm i -g aws-cdk`, **AWS CLI v2**, `dotnet tool install --global dotnet-ef`.
- Kiểm tra: `dotnet --version` (9.x), `docker --version`, `cdk --version`, `aws --version`.

---

## 3. Chạy TOÀN HỆ ở LOCAL (không tốn AWS)

Dùng Docker cho Postgres + **LocalStack** (giả lập SNS/SQS). **Bật Docker Desktop trước.**

### Bước 1 — Hạ tầng local
```bash
docker compose up -d postgres localstack
```
> Postgres map cổng host **5433** (5432 bị Postgres native trên Windows chiếm). LocalStack ở **4566**.

### Bước 2 — Tạo SNS topic + SQS queue + DLQ trên LocalStack
```powershell
./scripts/localstack-init.ps1
```
Lệnh này in ra `TopicArn` (dạng `arn:aws:sns:ap-southeast-1:000000000000:shipment-status-changed`).

### Bước 3 — Set biến môi trường (cho terminal sẽ chạy service)
```powershell
$env:AWS_ENDPOINT_URL = "http://localhost:4566"
$env:SNS_TOPIC_ARN    = "arn:aws:sns:ap-southeast-1:000000000000:shipment-status-changed"
```
> `AWS_ENDPOINT_URL` báo SDK trỏ vào LocalStack thay vì AWS thật. Không set thì SDK gọi AWS thật.

### Bước 4 — Chạy 3 service (mỗi cái 1 terminal, đều set env ở Bước 3)
```bash
dotnet run --project src/Services/Shipment/Shipment.Api --urls http://localhost:5080
```
```bash
dotnet run --project src/Services/Tracking/Tracking.Api --urls http://localhost:5081
```
```bash
dotnet run --project src/Services/Notification/Notification.Worker
```
> Mỗi service **tự chạy migration** lúc khởi động (tạo DB + bảng nếu chưa có). Connection mặc định trỏ Postgres local `localhost:5433`.

### Bước 5 — Thử nghiệm (terminal khác)
```powershell
# Tạo shipment
$c = Invoke-RestMethod -Method Post http://localhost:5080/shipments -ContentType application/json -Body (@{ origin=@{line="1 Le Loi";city="HCMC";postalCode="700000"}; destination=@{line="2 Hoan Kiem";city="Hanoi";postalCode="100000"} } | ConvertTo-Json)
$c.trackingCode

# Đổi trạng thái
Invoke-RestMethod -Method Post "http://localhost:5080/shipments/$($c.trackingCode)/status" -ContentType application/json -Body (@{action="pickedup"} | ConvertTo-Json)

# Tra cứu timeline (Tracking read-model, dựng từ event — chờ ~5-10s cho event chảy)
Invoke-RestMethod "http://localhost:5081/track/$($c.trackingCode)"
```
Notification Worker sẽ log dòng `Notification sent for <code>: status=...`.

Trạng thái hợp lệ để đổi: `pickedup → intransit → outfordelivery → delivered` (hoặc `failed → returned`). Chuyển sai → HTTP 400.

### Bước 6 — Tắt khi xong
```bash
docker compose down
```

---

## 4. Chạy trên AWS (deploy thật)

Cần: tài khoản AWS + `aws configure`. **Tài nguyên tính tiền** → nhớ `cdk destroy` sau khi xem.

```bash
cdk bootstrap aws://<account-id>/ap-southeast-1
```
Deploy theo thứ tự phụ thuộc (từ thư mục `infra/`):
```bash
cdk deploy Logistics-Network Logistics-Ecr Logistics-Data
```
```bash
cdk deploy Logistics-Compute
```
Đẩy image lên ECR (hoặc để CI làm — mục 5), rồi lấy URL ALB:
```bash
aws cloudformation describe-stacks --stack-name Logistics-Compute --query "Stacks[0].Outputs" --output table
```
Gọi thử: `http://<AlbDns>/shipments` và `/track/...`. Xong thì:
```bash
cdk destroy Logistics-Compute Logistics-Data
```
> Giữ Network + Ecr thì gần như free. `cdk destroy --all` để dọn sạch.

---

## 5. CI/CD (GitHub Actions) — M6

File: `.github/workflows/ci-cd.yml`. Khi `git push`:
- **build-test** (mọi push/PR): `dotnet restore/build/test`.
- **deploy** (chỉ `main`): OIDC assume role → login ECR → build & push 3 image → `ecs update-service --force-new-deployment`.

**Setup một lần:**
1. Deploy role OIDC: `cdk deploy Logistics-Cicd -c githubRepo=<owner>/<repo>` → copy `DeployRoleArn` ở output.
2. Trên GitHub repo → Settings → Secrets and variables → Actions → thêm secret **`AWS_DEPLOY_ROLE_ARN`** = ARN vừa copy.
3. Push code → xem tab **Actions**.

> OIDC = GitHub xin token ngắn hạn, AWS kiểm tra đúng repo rồi cho assume role tạm thời. **Không** lưu access key trong GitHub.

---

## 6. Bản đồ thư mục

```
src/Services/<Svc>/
  <Svc>.Domain          # entity, value object, state machine (thuần, 0 dependency)
  <Svc>.Application      # use case, port (interface), DTO
  <Svc>.Infrastructure  # EF (DbContext, repo), messaging (SNS/SQS), DI
  <Svc>.Api / .Worker   # host: endpoint / background consumer, composition root
src/Shared/
  BuildingBlocks.*       # Entity/AggregateRoot/ValueObject, base theo tầng
  Shared.Contracts       # integration event (hợp đồng giữa service)
infra/                   # AWS CDK (C#): Network/Ecr/Data/Compute/Cicd stacks
scripts/localstack-init.ps1   # bootstrap SNS/SQS local
docs/                    # AWS-WALKTHROUGH.md, RUN.md
tests/                   # unit test (state machine)
```

---

## 7. Khái niệm cốt lõi (ôn phỏng vấn)

- **Clean Architecture**: dependency chỉ hướng vào trong; Application định nghĩa port, Infrastructure hiện thực.
- **CQRS cấp service**: Shipment (ghi) tách khỏi Tracking (đọc/read-model).
- **Outbox pattern**: ghi event + dữ liệu trong 1 transaction → giải **dual-write**, không mất event.
- **SNS→SQS fan-out**: 1 event → nhiều consumer độc lập.
- **Idempotency**: consumer lọc message trùng (`processed_messages`) vì SQS giao **at-least-once**.
- **DLQ**: message lỗi N lần → hàng đợi chết, không kẹt queue chính.
- **Eventual consistency**: read-model cập nhật sau ghi một nhịp.
- **Database-per-service**: mỗi service DB riêng, không share schema.
- **IaC (CDK)** + **OIDC CI/CD**: hạ tầng bằng code, deploy không dùng key dài hạn.

---

## 8. Gỡ rối (lỗi đã gặp)

| Triệu chứng | Nguyên nhân & cách xử |
|---|---|
| `relation "..." does not exist` sau khi thêm migration | Chưa **rebuild host** sau `ef migrations add` → DLL thiếu migration. Rebuild; nếu DB kẹt: `DROP DATABASE <db>` rồi chạy lại |
| `Could not load ...EntityFrameworkCore.Relational 9.0.4` | Lệch version EF. Đã ghim đồng bộ 9.0.4 + khai báo Relational tường minh trong Infrastructure csproj |
| aws CLI `Could not connect to :4566` | LocalStack chưa sẵn sàng — chờ `http://localhost:4566/_localstack/health` trả 200 trước khi bootstrap |
| Port 5432 bận | Postgres native trên Windows chiếm; docker-compose map host **5433** |
| Container không kết nối | Docker Desktop chưa chạy — bật lên, đợi engine xanh |
