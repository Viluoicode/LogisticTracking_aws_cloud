using Logistics.Shipment.Application.Contracts;
using Logistics.Shipment.Application.Exceptions;
using Logistics.Shipment.Application.Features.CreateShipment;
using Logistics.Shipment.Application.Features.UpdateStatus;
using Xunit;
using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Application.Tests;

public class UpdateShipmentStatusHandlerTests
{
    private static async Task<(FakeShipmentRepository repo, FakeUnitOfWork uow, string code)> SeedAsync()
    {
        var repo = new FakeShipmentRepository();
        var uow = new FakeUnitOfWork();
        var code = await new CreateShipmentHandler(repo, uow).Handle(
            new CreateShipmentCommand(
                new AddressDto("1 A", "HCMC", "700000"),
                new AddressDto("2 B", "Hanoi", "100000")),
            CancellationToken.None);
        return (repo, uow, code);
    }

    [Fact]
    public async Task Valid_transition_updates_status()
    {
        var (repo, uow, code) = await SeedAsync();

        await new UpdateShipmentStatusHandler(repo, uow)
            .Handle(new UpdateShipmentStatusCommand(code, "pickedup"), CancellationToken.None);

        Assert.Equal(Domain.ShipmentStatus.PickedUp, repo.Items[0].Status);
    }

    [Fact]
    public async Task Unknown_code_throws_NotFound()
    {
        var handler = new UpdateShipmentStatusHandler(new FakeShipmentRepository(), new FakeUnitOfWork());

        await Assert.ThrowsAsync<ShipmentNotFoundException>(() =>
            handler.Handle(new UpdateShipmentStatusCommand("LTNOPE", "pickedup"), CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_transition_throws()
    {
        var (repo, uow, code) = await SeedAsync();
        var handler = new UpdateShipmentStatusHandler(repo, uow);

        await Assert.ThrowsAsync<Domain.InvalidShipmentTransitionException>(() =>
            handler.Handle(new UpdateShipmentStatusCommand(code, "delivered"), CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_action_throws_ArgumentException()
    {
        var (repo, uow, code) = await SeedAsync();
        var handler = new UpdateShipmentStatusHandler(repo, uow);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new UpdateShipmentStatusCommand(code, "teleport"), CancellationToken.None));
    }
}
