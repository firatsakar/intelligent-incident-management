using IdentityService.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure.Persistence;

/// <summary>
/// Drops refresh tokens that have been expired long enough to prove nothing.
/// </summary>
/// <remarks>
/// Rotation writes a new row on every refresh, so at a fifteen-minute access token this table
/// gains a row per session per quarter of an hour. Left alone it grows for as long as the product
/// is used, and the one query on the request path — find a token by its hash — pays for all of it.
/// The shape is <c>OutboxCleanupService</c>'s, for the same reason.
/// </remarks>
public sealed class RefreshTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);

    // Kept past expiry so that "why was I signed out" has an answer for a while. A row this old
    // cannot authorise anything; what it still holds is the record of when it stopped being able
    // to, and after a month nobody is asking.
    private static readonly TimeSpan Grace = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenCleanupService> _logger;

    public RefreshTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<RefreshTokenCleanupService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

                var removed = await tokens.DeleteExpiredBeforeAsync(
                    DateTime.UtcNow - Grace,
                    stoppingToken
                );

                if (removed > 0)
                    _logger.LogInformation("Removed {Count} expired refresh token(s).", removed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token cleanup failed. Retrying next sweep.");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }
}
