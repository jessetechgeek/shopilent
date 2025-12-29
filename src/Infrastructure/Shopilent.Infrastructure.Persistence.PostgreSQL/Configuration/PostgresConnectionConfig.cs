using Microsoft.Extensions.Logging;
using Npgsql;
using Shopilent.Application.Abstractions.Caching;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Configuration;

public class PostgresConnectionConfig
{
    private readonly ILogger<PostgresConnectionConfig> _logger;
    private readonly ICacheService? _cacheService;

    // Configuration
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _circuitBreakerTimeout = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _healthCheckTimeout = TimeSpan.FromSeconds(3);

    private int _currentReadReplicaIndex = 0;

    public string WriteConnectionString { get; set; } = string.Empty;
    public List<string> ReadConnectionStrings { get; set; } = new List<string>();

    public PostgresConnectionConfig(
        ILogger<PostgresConnectionConfig> logger = null,
        ICacheService cacheService = null)
    {
        _logger = logger;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Gets a read connection string with health-aware load balancing.
    /// Falls back to write connection if no healthy replicas are available.
    /// Uses Redis-based distributed health checking when ICacheService is available.
    /// Falls back to simple round-robin when Redis is not available.
    /// </summary>
    public string GetReadConnectionString()
    {
        if (ReadConnectionStrings == null || !ReadConnectionStrings.Any())
        {
            _logger?.LogDebug("No read replicas configured, using write connection");
            return WriteConnectionString;
        }

        // If Redis cache is not available, fall back to simple round-robin
        if (_cacheService == null)
        {
            _logger?.LogDebug("Cache service not available, using simple round-robin");
            return GetReadConnectionStringRoundRobin();
        }

        // Use async version synchronously (acceptable for this use case)
        try
        {
            return GetReadConnectionStringAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error getting read connection with health check, falling back to round-robin");
            return GetReadConnectionStringRoundRobin();
        }
    }

    /// <summary>
    /// Async version with Redis-based health checking for use in async contexts.
    /// </summary>
    public async Task<string> GetReadConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (ReadConnectionStrings == null || !ReadConnectionStrings.Any())
        {
            _logger?.LogDebug("No read replicas configured, using write connection");
            return WriteConnectionString;
        }

        // If no cache service, fall back to round-robin
        if (_cacheService == null)
        {
            return GetReadConnectionStringRoundRobin();
        }

        // Try to find a healthy connection using distributed health state
        var startIndex = _currentReadReplicaIndex;

        for (int i = 0; i < ReadConnectionStrings.Count; i++)
        {
            var index = (startIndex + i) % ReadConnectionStrings.Count;
            var connectionString = ReadConnectionStrings[index];

            if (await IsReplicaHealthyAsync(index, connectionString, cancellationToken))
            {
                // Update round-robin index for next call
                _currentReadReplicaIndex = (index + 1) % ReadConnectionStrings.Count;
                return connectionString;
            }
        }

        // If no healthy read replica, fall back to write connection
        _logger?.LogWarning("All read replicas are unhealthy, falling back to write connection");
        return WriteConnectionString;
    }

    private string GetReadConnectionStringRoundRobin()
    {
        var index = Interlocked.Increment(ref _currentReadReplicaIndex) % ReadConnectionStrings.Count;
        _logger?.LogDebug("Using read replica {ReplicaIndex} (round-robin, no health check)", index);
        return ReadConnectionStrings[index];
    }

    private async Task<bool> IsReplicaHealthyAsync(
        int replicaIndex,
        string connectionString,
        CancellationToken cancellationToken)
    {
        var cacheKeyHealth = $"postgres:replica:{replicaIndex}:health";
        var cacheKeyCircuit = $"postgres:replica:{replicaIndex}:circuit";
        var lockKey = $"postgres:replica:{replicaIndex}:lock";

        var now = DateTime.UtcNow;

        try
        {
            // Step 1: Check circuit breaker (distributed across all instances)
            var circuitState = await _cacheService.GetAsync<CircuitBreakerState>(
                cacheKeyCircuit, cancellationToken);

            if (circuitState?.IsOpen == true && now < circuitState.OpenUntil)
            {
                _logger?.LogDebug(
                    "Replica {ReplicaIndex} circuit breaker is open until {Until}, skipping",
                    replicaIndex, circuitState.OpenUntil);
                return false;
            }

            // Step 2: Check cached health status
            var healthInfo = await _cacheService.GetAsync<ReplicaHealthInfo>(
                cacheKeyHealth, cancellationToken);

            // If we have recent health data, use it
            if (healthInfo?.LastCheckTime != null &&
                now - healthInfo.LastCheckTime.Value < _healthCheckInterval)
            {
                _logger?.LogDebug(
                    "Replica {ReplicaIndex} cached health: {IsHealthy} (age: {AgeSeconds}s)",
                    replicaIndex,
                    healthInfo.IsHealthy,
                    (now - healthInfo.LastCheckTime.Value).TotalSeconds);

                return healthInfo.IsHealthy;
            }

            // Step 3: Need to perform health check - try to acquire distributed lock
            var hasLock = await TryAcquireLockAsync(lockKey, cancellationToken);

            if (!hasLock)
            {
                // Another instance is checking health right now
                // Use stale cached value or assume unhealthy to be safe
                _logger?.LogDebug(
                    "Replica {ReplicaIndex} health check in progress by another instance, using cached value",
                    replicaIndex);

                return healthInfo?.IsHealthy ?? false;
            }

            try
            {
                // Step 4: Perform the actual health check
                var isHealthy = await PerformHealthCheckAsync(
                    replicaIndex, connectionString, cancellationToken);

                // Step 5: Update distributed cache with results
                var newHealthInfo = new ReplicaHealthInfo
                {
                    IsHealthy = isHealthy,
                    LastCheckTime = now,
                    ConsecutiveFailures = isHealthy
                        ? 0
                        : (healthInfo?.ConsecutiveFailures ?? 0) + 1
                };

                await _cacheService.SetAsync(
                    cacheKeyHealth,
                    newHealthInfo,
                    _healthCheckInterval,
                    cancellationToken);

                // Step 6: Update circuit breaker state
                if (!isHealthy)
                {
                    // Open circuit breaker
                    var newCircuitState = new CircuitBreakerState
                    {
                        IsOpen = true,
                        OpenUntil = now.Add(_circuitBreakerTimeout),
                        ConsecutiveFailures = newHealthInfo.ConsecutiveFailures
                    };

                    await _cacheService.SetAsync(
                        cacheKeyCircuit,
                        newCircuitState,
                        _circuitBreakerTimeout,
                        cancellationToken);

                    _logger?.LogWarning(
                        "Replica {ReplicaIndex} marked unhealthy. Circuit breaker opened for {Duration}. Consecutive failures: {Failures}",
                        replicaIndex, _circuitBreakerTimeout, newHealthInfo.ConsecutiveFailures);
                }
                else
                {
                    // Close circuit breaker
                    await _cacheService.RemoveAsync(cacheKeyCircuit, cancellationToken);

                    _logger?.LogInformation(
                        "Replica {ReplicaIndex} is healthy. Circuit breaker closed.",
                        replicaIndex);
                }

                return isHealthy;
            }
            finally
            {
                await ReleaseLockAsync(lockKey, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error checking replica {ReplicaIndex} health from cache. Assuming unhealthy for safety.",
                replicaIndex);

            // On cache failure, fail safe (assume unhealthy to avoid timeout)
            return false;
        }
    }

    private async Task<bool> PerformHealthCheckAsync(
        int replicaIndex,
        string connectionString,
        CancellationToken cancellationToken)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var serverInfo = $"Host={builder.Host}, Port={builder.Port}, Database={builder.Database}";

            await using var connection = new NpgsqlConnection(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = (int)_healthCheckTimeout.TotalSeconds;

            await cmd.ExecuteScalarAsync(cancellationToken);

            _logger?.LogDebug(
                "Health check PASSED for replica {ReplicaIndex}: {ServerInfo}",
                replicaIndex, serverInfo);

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning(
                "Health check TIMEOUT for replica {ReplicaIndex}",
                replicaIndex);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Health check FAILED for replica {ReplicaIndex}: {Error}",
                replicaIndex, ex.Message);
            return false;
        }
    }

    private async Task<bool> TryAcquireLockAsync(string lockKey, CancellationToken cancellationToken)
    {
        try
        {
            // Check if lock exists
            var lockExists = await _cacheService.ExistsAsync(lockKey, cancellationToken);

            if (lockExists)
            {
                return false; // Lock already held by another instance
            }

            // Try to acquire lock with 10-second TTL
            var lockValue = Guid.NewGuid().ToString();
            await _cacheService.SetAsync(lockKey, lockValue, TimeSpan.FromSeconds(10), cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error acquiring distributed lock for {LockKey}", lockKey);
            return false;
        }
    }

    private async Task ReleaseLockAsync(string lockKey, CancellationToken cancellationToken)
    {
        try
        {
            await _cacheService.RemoveAsync(lockKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error releasing distributed lock for {LockKey}. Lock will expire automatically.", lockKey);
            // Ignore - lock will expire automatically
        }
    }

    /// <summary>
    /// Data classes for distributed state
    /// </summary>
    private class ReplicaHealthInfo
    {
        public bool IsHealthy { get; set; } = true;
        public DateTime? LastCheckTime { get; set; }
        public int ConsecutiveFailures { get; set; }
    }

    private class CircuitBreakerState
    {
        public bool IsOpen { get; set; }
        public DateTime OpenUntil { get; set; }
        public int ConsecutiveFailures { get; set; }
    }
}
