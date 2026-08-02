using Logistics.Tracking.Application.Abstractions;
using Logistics.Tracking.Application.Contracts;
using Logistics.Tracking.Infrastructure;
using Logistics.Tracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddTrackingInfrastructure(); // DbContext + read store + SQS consumer

var app = builder.Build();

// Auto-migrate (dev/demo). Tạo DB "tracking" nếu chưa có (database-per-service).
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<TrackingDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { service = "tracking", status = "healthy" }));

// Tra cứu timeline trạng thái theo mã vận đơn (đọc từ read-model dựng bởi consumer).
app.MapGet("/track/{code}", async (string code, ITrackingReadStore store, CancellationToken ct) =>
{
    var entries = await store.GetTimelineAsync(code, ct);
    if (entries.Count == 0) return Results.NotFound();

    var timeline = entries.Select(e => new TrackingPointDto(e.Status, e.OccurredOnUtc)).ToList();
    return Results.Ok(new TrackingTimelineResponse(code, entries[^1].Status, timeline));
});

app.Run();
