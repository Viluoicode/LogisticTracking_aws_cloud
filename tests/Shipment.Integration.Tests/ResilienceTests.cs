using Logistics.BuildingBlocks.Infrastructure.Resilience;
using Xunit;

namespace Logistics.Shipment.Integration.Tests;

/// <summary>Chứng minh pipeline retry lỗi thoáng qua rồi thành công (không cần DB).</summary>
public class ResilienceTests
{
    [Fact]
    public async Task Pipeline_retries_transient_failure_then_succeeds()
    {
        var pipeline = MessagingResilience.Build();
        var attempts = 0;

        await pipeline.ExecuteAsync(async _ =>
        {
            attempts++;
            if (attempts < 3) throw new TimeoutException("transient");
            await Task.CompletedTask;
        });

        Assert.Equal(3, attempts); // 1 lần đầu + 2 retry
    }
}
