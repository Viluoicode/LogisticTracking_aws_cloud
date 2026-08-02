using FluentValidation;

namespace Logistics.Shipment.Application.Features.UpdateStatus;

public sealed class UpdateShipmentStatusValidator : AbstractValidator<UpdateShipmentStatusCommand>
{
    private static readonly string[] Allowed =
        ["pickedup", "intransit", "outfordelivery", "delivered", "failed", "returned"];

    public UpdateShipmentStatusValidator()
    {
        RuleFor(x => x.TrackingCode).NotEmpty();
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => Allowed.Contains(a.Trim().ToLowerInvariant()))
            .WithMessage("Action must be one of: " + string.Join(", ", Allowed));
    }
}
