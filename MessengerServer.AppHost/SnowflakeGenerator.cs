

namespace MessengerServer.AppHost
{
    internal class SnowflakeGenerator
    {
        private const ulong Epoch = 1700000000000UL;
        private const int WorkerIdBits = 10;
        private const int SequenceBits = 12;

        private const ulong MaxWorkerId = (1UL << WorkerIdBits) - 1;
        private const ulong MaxSequence = (1UL << SequenceBits) - 1;

        private readonly Lock _lock = new();
        private readonly ulong _workerId;

        private ulong _lastTimestamp = 0;
        private ulong _sequence = 0;

        public SnowflakeGenerator(ulong workerId)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(workerId, MaxWorkerId);
            _workerId = workerId;
        }

        public ulong GenerateId()
        {
            lock (_lock)
            {
                ulong timestamp = GetCurrentTimestamp();

                if (timestamp < _lastTimestamp)
                    throw new Exception("Clock moved backwards!");

                if (timestamp == _lastTimestamp)
                {
                    _sequence = (_sequence + 1) & MaxSequence;
                    if (_sequence == 0)
                        timestamp = WaitNextMillis(timestamp);
                }
                else
                {
                    _sequence = 0;
                }

                _lastTimestamp = timestamp;

                return ((timestamp - Epoch) << (WorkerIdBits + SequenceBits))
                       | (_workerId << SequenceBits)
                       | _sequence;
            }
        }

        private ulong WaitNextMillis(ulong timestamp)
        {
            while (timestamp <= _lastTimestamp)
                timestamp = GetCurrentTimestamp();

            return timestamp;
        }

        private static ulong GetCurrentTimestamp()
            => (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
