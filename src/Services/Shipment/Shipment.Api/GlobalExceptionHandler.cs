using FluentValidation;
using Logistics.Shipment.Application.Exceptions;
using Logistics.Shipment.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Shipment.Api;

/// <summary>Map exception -> HTTP status ở một chỗ (thay vì try/catch rải rác trong endpoint).</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            ValidationException                 => (400, "Validation failed"),
            ArgumentException                   => (400, "Invalid argument"),
            InvalidShipmentTransitionException  => (400, "Invalid shipment transition"),
            ShipmentNotFoundException           => (404, "Shipment not found"),
            DbUpdateConcurrencyException        => (409, "Shipment was modified by someone else, retry"),
            _                                   => (500, "Internal server error")
        };

        if (status == 500)
            logger.LogError(ex, "Unhandled exception");

        object[]? errors = ex is ValidationException ve
            ? ve.Errors.Select(e => (object)new { e.PropertyName, e.ErrorMessage }).ToArray()
            : null;

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new
        {
            title,
            status,
            errors,
            detail = status < 500 ? ex.Message : null // không lộ chi tiết lỗi 500
        }, ct);

        return true;
    }
}
