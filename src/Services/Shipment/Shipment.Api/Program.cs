using Logistics.Shipment.Application;
using Logistics.Shipment.Application.Abstractions;
using Logistics.Shipment.Application.Contracts;
using Logistics.Shipment.Application.Exceptions;
using Logistics.Shipment.Application.Features.CreateShipment;
using Logistics.Shipment.Application.Features.GetShipment;
using Logistics.Shipment.Application.Features.UpdateStatus;
using Logistics.Shipment.Domain;
using Logistics.Shipment.Infrastructure;
using Logistics.Shipment.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// M7: log có cấu trúc (Serilog) — trên AWS đẩy vào CloudWatch Logs.
builder.Host.UseSerilog((context, config) => config.WriteTo.Console());

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Logistics.Shipment.Api.GlobalExceptionHandler>();
builder.Services.AddShipmentApplication();
builder.Services.AddShipmentInfrastructure();

// M7: health check kiểm tra kết nối DB (ALB dùng để biết task còn sống).
builder.Services.AddHealthChecks().AddDbContextCheck<ShipmentDbContext>();

// M7: distributed tracing (OpenTelemetry). Local -> console; AWS -> đổi exporter sang OTLP/X-Ray.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("shipment-api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// A2: JWT Bearer auth. DEMO dùng key đối xứng; PROD đổi sang Cognito (Authority + JWKS, không shared secret).
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-only-demo-signing-key-please-change-32bytes!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "logistics";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "logistics-clients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-migrate lúc khởi động (dev/demo; desiredCount=1 nên không đua migration).
// Prod nhiều instance nên tách bước migrate riêng (job/CI) — ghi chú để nâng cấp sau.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler(); // map exception -> HTTP status (validation 400, not-found 404...)

app.UseSerilogRequestLogging(); // mỗi HTTP request -> 1 dòng log có cấu trúc

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// M7: health check thật — 200 nếu DB reachable, 503 nếu không (ALB dựa vào đây).
app.MapHealthChecks("/health");

// Tạo shipment mới
app.MapPost("/shipments", async (CreateShipmentRequest req, ISender sender, IIdempotencyStore idem, HttpContext http, CancellationToken ct) =>
{
    // B11: Idempotency-Key -> POST lặp (client retry) trả cùng kết quả, không tạo trùng.
    var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(key))
    {
        var existing = await idem.GetTrackingCodeAsync(key, ct);
        if (existing is not null)
            return Results.Ok(new { trackingCode = existing, idempotentReplay = true });
    }

    var code = await sender.Send(new CreateShipmentCommand(req.Origin, req.Destination), ct);

    if (!string.IsNullOrWhiteSpace(key))
        await idem.SaveAsync(key, code, ct);

    return Results.Created($"/shipments/{code}", new { trackingCode = code });
}).RequireAuthorization();

// Tra cứu shipment theo mã
app.MapGet("/shipments/{code}", async (string code, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(new GetShipmentQuery(code), ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

// Đổi trạng thái (pickedup/intransit/outfordelivery/delivered/failed/returned)
app.MapPost("/shipments/{code}/status", async (string code, UpdateStatusRequest req, ISender sender, CancellationToken ct) =>
{
    await sender.Send(new UpdateShipmentStatusCommand(code, req.Action), ct);
    return Results.NoContent();
    // Exception (validation / not-found / invalid-transition) -> GlobalExceptionHandler map status.
}).RequireAuthorization();

// A2 (DEV ONLY): mint JWT để test. Prod: token do Cognito cấp, KHÔNG có endpoint này.
if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/token", () =>
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwtIssuer, jwtAudience,
            [new Claim(ClaimTypes.NameIdentifier, "demo-user")],
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: creds);
        return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    });
}

app.Run();

// Request bodies cho endpoint
record CreateShipmentRequest(AddressDto Origin, AddressDto Destination);
record UpdateStatusRequest(string Action);
