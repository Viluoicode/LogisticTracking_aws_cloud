using Logistics.Shipment.Application.Contracts;
using Logistics.Shipment.Application.Features.CreateShipment;
using Logistics.Shipment.Application.Features.GetShipment;
using Xunit;

namespace Logistics.Shipment.Application.Tests;

public class GetShipmentHandlerTests
{
    [Fact]
    public async Task Returns_timeline_for_existing()
    {
        var repo = new FakeShipmentRepository();
        var uow = new FakeUnitOfWork();
        var code = await new CreateShipmentHandler(repo, uow).Handle(
            new CreateShipmentCommand(
                new AddressDto("1 A", "HCMC", "700000"),
                new AddressDto("2 B", "Hanoi", "100000")),
            CancellationToken.None);

        var result = await new GetShipmentHandler(repo).Handle(new GetShipmentQuery(code), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(code, result!.Code);
        Assert.Equal("Created", result.Status);
        Assert.Single(result.History);
    }

    [Fact]
    public async Task Returns_null_for_unknown()
    {
        var result = await new GetShipmentHandler(new FakeShipmentRepository())
            .Handle(new GetShipmentQuery("LTNOPE"), CancellationToken.None);

        Assert.Null(result);
    }
}
