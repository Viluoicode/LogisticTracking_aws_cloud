using Microsoft.EntityFrameworkCore;
using Xunit;
using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Integration.Tests;

public class ShipmentPersistenceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static Domain.Shipment NewShipment() =>
        Domain.Shipment.Create(
            new Domain.Address("1 A", "HCMC", "700000"),
            new Domain.Address("2 B", "Hanoi", "100000"));

    [Fact]
    public async Task Roundtrip_persists_and_reads_via_value_object_query()
    {
        var shipment = NewShipment();
        await using (var db = fx.CreateContext())
        {
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        var loaded = await read.Shipments.FirstOrDefaultAsync(s => s.Code == shipment.Code);

        Assert.NotNull(loaded);
        Assert.Equal(Domain.ShipmentStatus.Created, loaded!.Status);
        Assert.Equal("HCMC", loaded.Origin.City);   // owned type map đúng
    }

    [Fact]
    public async Task Concurrent_update_throws_DbUpdateConcurrencyException()
    {
        var shipment = NewShipment();
        await using (var db = fx.CreateContext())
        {
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();
        }

        // Hai context cùng load một row (cùng xmin)
        await using var ctx1 = fx.CreateContext();
        await using var ctx2 = fx.CreateContext();
        var s1 = await ctx1.Shipments.FirstAsync(s => s.Code == shipment.Code);
        var s2 = await ctx2.Shipments.FirstAsync(s => s.Code == shipment.Code);

        s1.MarkPickedUp();
        await ctx1.SaveChangesAsync();   // thắng -> xmin đổi

        s2.MarkPickedUp();
        // xmin đã cũ -> update 0 row -> optimistic concurrency chặn
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());
    }
}
