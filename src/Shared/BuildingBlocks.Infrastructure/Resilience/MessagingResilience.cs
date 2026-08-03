using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Logistics.BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Pipeline chịu lỗi dùng chung cho call hạ tầng ngoài (SNS/SQS):
/// Retry (exponential backoff + jitter) -> Circuit Breaker -> Timeout.
/// - Retry: xử lý lỗi thoáng qua (mạng chập) mà không dồn dập.
/// - Circuit breaker: dịch vụ hỏng liên tục -> "ngắt cầu dao" để khỏi cascade.
/// - Timeout: mọi call ngoài phải có hạn, tránh treo thread vô hạn.
/// </summary>
public static class MessagingResilience
{
    public static ResiliencePipeline Build() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15)
            })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();
}
