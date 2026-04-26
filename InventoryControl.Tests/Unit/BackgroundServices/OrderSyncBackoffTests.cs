
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryControl.Tests.Unit.BackgroundServices;

public class OrderSyncBackoffTests
{
    [Theory]
    [InlineData(0, 15)]  // base interval — no failures
    [InlineData(1, 30)]  // 15 * 2^1 = 30, at cap
    [InlineData(2, 30)]  // capped at 30 min
    [InlineData(5, 30)]  // still capped
    [InlineData(10, 30)] // still capped
    public void CalculateBackoffDelay_ReturnsCorrectDelay(int consecutiveFailures, int expectedMinutes)
    {
        var delay = OrderSyncBackgroundService.CalculateBackoffDelay(consecutiveFailures);
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), delay);
    }

    [Fact]
    public void CalculateBackoffDelay_ZeroFailures_ReturnsBaseInterval()
    {
        var delay = OrderSyncBackgroundService.CalculateBackoffDelay(0);
        Assert.Equal(TimeSpan.FromMinutes(15), delay);
    }

    [Fact]
    public void CalculateBackoffDelay_NegativeFailures_ReturnsBaseInterval()
    {
        var delay = OrderSyncBackgroundService.CalculateBackoffDelay(-1);
        Assert.Equal(TimeSpan.FromMinutes(15), delay);
    }
}
