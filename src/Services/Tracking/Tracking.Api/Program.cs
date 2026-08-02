using Logistics.Tracking.Application.Abstractions;
using Logistics.Tracking.Application.Contracts;
using Logistics.Tracking.Infrastructure;
using Logistics.Tracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// M7: log có cấu trúc (Serilog).
builder.Host.UseSerilog((context, config) => config.WriteTo.Console());

builder.Services.AddOpenApi();
builder.Services.AddTrackingInfrastructure(); // DbContext + read store + SQS consumer
builder.Services.AddHealthChecks().AddDbContextCheck<TrackingDbContext>();

// M7: distributed tracing (OpenTelemetry). Local -> console; AWS -> đổi exporter sang OTLP/X-Ray.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("tracking-api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

// Auto-migrate (dev/demo). Tạo DB "tracking" nếu chưa có (database-per-service).
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<TrackingDbContext>().Database.Migrate();
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// M7: health check thật — kiểm DB.
app.MapHealthChecks("/health");

// Tra cứu timeline trạng thái theo mã vận đơn (đọc từ read-model dựng bởi consumer).
app.MapGet("/track/{code}", async (string code, ITrackingReadStore store, CancellationToken ct) =>
{
    var entries = await store.GetTimelineAsync(code, ct);
    if (entries.Count == 0) return Results.NotFound();

    var timeline = entries.Select(e => new TrackingPointDto(e.Status, e.OccurredOnUtc)).ToList();
    return Results.Ok(new TrackingTimelineResponse(code, entries[^1].Status, timeline));
});

app.Run();
