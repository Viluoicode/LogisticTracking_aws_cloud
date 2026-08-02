using FluentValidation;
using Logistics.Shipment.Application.Contracts;

namespace Logistics.Shipment.Application.Features.CreateShipment;

public sealed class CreateShipmentValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentValidator()
    {
        RuleFor(x => x.Origin).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(x => x.Destination).NotNull().SetValidator(new AddressDtoValidator());
    }
}

public sealed class AddressDtoValidator : AbstractValidator<AddressDto>
{
    public AddressDtoValidator()
    {
        RuleFor(x => x.Line).NotEmpty().MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(20);
    }
}
