using Logistics.Shipment.Application;
using Logistics.Shipment.Application.Contracts;
using Logistics.Shipment.Application.Exceptions;
using Logistics.Shipment.Application.Features.CreateShipment;
using Logistics.Shipment.Application.Features.GetShipment;
using Logistics.Shipment.Application.Features.UpdateStatus;
using Logistics.Shipment.Domain;
using Logistics.Shipment.Infrastructure;
using Logistics.Shipment.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// M7: log có cấu trúc (Serilog) — trên AWS đẩy vào CloudWatch Logs.
builder.Host.UseSerilog((context, config) => config.WriteTo.Console());

builder.Services.AddOpenApi();
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

var app = builder.Build();

// Auto-migrate lúc khởi động (dev/demo; desiredCount=1 nên không đua migration).
// Prod nhiều instance nên tách bước migrate riêng (job/CI) — ghi chú để nâng cấp sau.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
    db.Database.Migrate();
}

app.UseSerilogRequestLogging(); // mỗi HTTP request -> 1 dòng log có cấu trúc

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// M7: health check thật — 200 nếu DB reachable, 503 nếu không (ALB dựa vào đây).
app.MapHealthChecks("/health");

// Tạo shipment mới
app.MapPost("/shipments", async (CreateShipmentRequest req, ISender sender, CancellationToken ct) =>
{
    var code = await sender.Send(new CreateShipmentCommand(req.Origin, req.Destination), ct);
    return Results.Created($"/shipments/{code}", new { trackingCode = code });
});

// Tra cứu shipment theo mã
app.MapGet("/shipments/{code}", async (string code, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(new GetShipmentQuery(code), ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

// Đổi trạng thái (pickedup/intransit/outfordelivery/delivered/failed/returned)
app.MapPost("/shipments/{code}/status", async (string code, UpdateStatusRequest req, ISender sender, CancellationToken ct) =>
{
    try
    {
        await sender.Send(new UpdateShipmentStatusCommand(code, req.Action), ct);
        return Results.NoContent();
    }
    catch (ShipmentNotFoundException) { return Results.NotFound(); }
    catch (InvalidShipmentTransitionException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.Run();

// Request bodies cho endpoint
record CreateShipmentRequest(AddressDto Origin, AddressDto Destination);
record UpdateStatusRequest(string Action);
