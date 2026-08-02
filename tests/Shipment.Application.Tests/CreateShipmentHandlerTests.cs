using Logistics.Shipment.Application.Contracts;
using Logistics.Shipment.Application.Features.CreateShipment;
using Xunit;

namespace Logistics.Shipment.Application.Tests;

public class CreateShipmentHandlerTests
{
    [Fact]
    public async Task Creates_shipment_saves_and_returns_code()
    {
        var repo = new FakeShipmentRepository();
        var uow = new FakeUnitOfWork();
        var handler = new CreateShipmentHandler(repo, uow);

        var code = await handler.Handle(
            new CreateShipmentCommand(
                new AddressDto("1 A", "HCMC", "700000"),
                new AddressDto("2 B", "Hanoi", "100000")),
            CancellationToken.None);

        Assert.StartsWith("LT", code);
        Assert.Single(repo.Items);
        Assert.Equal(1, uow.SaveCount);
    }
}
