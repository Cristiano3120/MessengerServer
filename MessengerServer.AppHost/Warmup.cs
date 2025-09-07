using MessengerServer.AppHost.Logging;
using Npgsql;
using System.Diagnostics;

namespace MessengerServer.AppHost
{
    internal static class Warmup
    {
        public static async Task WarmupDbPoolAsync(string connString, int count = 5, int queriesPerConn = 1000)
        {
            Logger.Log("Starting DB pool warmup...");
            Stopwatch stopwatch = Stopwatch.StartNew();

            List<NpgsqlConnection> conns = [];

            for (int i = 0; i < count; i++)
            {
                NpgsqlConnection conn = new(connString);
                await conn.OpenAsync();
                conns.Add(conn);

                for (int q = 0; q < queriesPerConn; q++)
                {
                    await using NpgsqlCommand cmd = new("SELECT 1;", conn);
                    _ = await cmd.ExecuteScalarAsync();
                }
            }

            foreach (NpgsqlConnection conn in conns)
                await conn.CloseAsync();

            stopwatch.Stop();

            double totalQueries = count * queriesPerConn;
            double avgTimePerQueryMs = stopwatch.Elapsed.TotalMilliseconds / totalQueries;

            Logger.Log($"Warmup finished. Total time: {stopwatch.Elapsed.TotalSeconds:F3}s");
            Logger.Log($"Average time per query: {avgTimePerQueryMs:F4} ms");

            await Task.Delay(500);
        }

        public static async Task WarmupCryptographyAsync()
        {
            Logger.Log("Starting Cryptography warmup...");

            const int runs = 3;
            Stopwatch stopwatchTotal = Stopwatch.StartNew();
            double totalElapsedMs = 0;

            for (int i = 0; i < runs; i++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                _ = DatabaseCryptography.HashDeterministic("testHash123456789!");
                _ = DatabaseCryptography.HashNonDeterministic("testHash123456789!");
                byte[] data = DatabaseCryptography.Encrypt("test@example.com");
                _ = DatabaseCryptography.Decrypt(data);

                stopwatch.Stop();
                totalElapsedMs += stopwatch.Elapsed.TotalMilliseconds;
            }

            stopwatchTotal.Stop();

            double avgTimeMs = totalElapsedMs / runs;

            Logger.Log($"Cryptography warmup finished. Total time: {stopwatchTotal.Elapsed.TotalMilliseconds:F3}ms");
            Logger.Log($"Average time per run: {avgTimeMs:F3}ms");

            await Task.Delay(1000);
        }
    }
}
