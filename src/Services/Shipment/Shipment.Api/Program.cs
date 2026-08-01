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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddShipmentApplication();
builder.Services.AddShipmentInfrastructure();

var app = builder.Build();

// Auto-migrate lúc khởi động (dev/demo; desiredCount=1 nên không đua migration).
// Prod nhiều instance nên tách bước migrate riêng (job/CI) — ghi chú để nâng cấp sau.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Health cho ALB target group (gọi thẳng /health trên container).
app.MapGet("/health", () => Results.Ok(new { service = "shipment", status = "healthy" }));

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
