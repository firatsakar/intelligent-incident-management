using Npgsql;

namespace TelemetryIngestionService.Infrastructure.Ingestion;

/// <summary>
/// One replica polls a source at a time (Adım 30): a Postgres advisory lock on the source, held on a
/// connection of its own for the length of one poll.
/// </summary>
/// <remarks>
/// <para>
/// Without it two replicas poll the same log store on the same tick. The data survives that — the
/// unique index on the source's event id refuses the second copy — but every record is fetched and
/// refused twice, and the source is asked twice as often as its interval says.
/// </para>
/// <para>
/// A session lock rather than a row lock, because a poll writes through its own contexts and
/// commits as it goes; there is no one transaction to hold a row lock in. The connection is not the
/// pool's to keep: disposing unlocks, and a process that dies takes its sessions — and so its
/// locks — with it.
/// </para>
/// </remarks>
public sealed class SourcePollLock : IAsyncDisposable
{
    // The first key says what kind of thing is locked, so these never meet another advisory lock.
    private const int TelemetryPoll = 0x49494D01;

    private readonly NpgsqlConnection _connection;
    private readonly int _key;

    private SourcePollLock(NpgsqlConnection connection, int key)
    {
        _connection = connection;
        _key = key;
    }

    /// <summary>The lock, or null when another replica is polling this source right now.</summary>
    public static async Task<SourcePollLock?> TryAcquireAsync(
        string connectionString,
        Guid sourceId,
        CancellationToken cancellationToken = default
    )
    {
        var connection = new NpgsqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);

            var key = KeyFor(sourceId);

            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@kind, @key)", connection);
            command.Parameters.AddWithValue("kind", TelemetryPoll);
            command.Parameters.AddWithValue("key", key);

            if (await command.ExecuteScalarAsync(cancellationToken) is true)
                return new SourcePollLock(connection, key);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        await connection.DisposeAsync();

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@kind, @key)", _connection);
            command.Parameters.AddWithValue("kind", TelemetryPoll);
            command.Parameters.AddWithValue("key", _key);
            await command.ExecuteScalarAsync();
        }
        finally
        {
            await _connection.DisposeAsync();
        }
    }

    // Stable across processes, which string.GetHashCode is not. Two sources that happen to share a
    // key only ever cost one of them a tick.
    private static int KeyFor(Guid sourceId)
    {
        Span<byte> bytes = stackalloc byte[16];
        sourceId.TryWriteBytes(bytes);

        return BitConverter.ToInt32(bytes[..4])
            ^ BitConverter.ToInt32(bytes[4..8])
            ^ BitConverter.ToInt32(bytes[8..12])
            ^ BitConverter.ToInt32(bytes[12..]);
    }
}
