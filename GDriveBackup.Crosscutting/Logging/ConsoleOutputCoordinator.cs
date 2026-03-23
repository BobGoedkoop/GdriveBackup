using System;
using System.Threading;

namespace GDriveBackup.Crosscutting.Logging
{
    public static class ConsoleOutputCoordinator
    {
        private static readonly object SyncRoot = new object();
        private static int _heartbeatActive;
        private static int _heartbeatRenderedSinceLastLog;

        public static void SetHeartbeatActive(bool isActive)
        {
            Interlocked.Exchange(ref _heartbeatActive, isActive ? 1 : 0);
            if (!isActive)
            {
                Interlocked.Exchange(ref _heartbeatRenderedSinceLastLog, 0);
            }
        }

        public static void WriteHeartbeat(string line)
        {
            lock (SyncRoot)
            {
                Console.Write(line);
                Interlocked.Exchange(ref _heartbeatRenderedSinceLastLog, 1);
            }
        }

        public static void ClearHeartbeatLine()
        {
            lock (SyncRoot)
            {
                Console.Write("\r");
                Interlocked.Exchange(ref _heartbeatRenderedSinceLastLog, 0);
            }
        }

        public static void PrepareForLogLine()
        {
            if (Interlocked.CompareExchange(ref _heartbeatActive, 0, 0) == 0)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _heartbeatRenderedSinceLastLog, 0, 0) == 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                Console.WriteLine();
                Interlocked.Exchange(ref _heartbeatRenderedSinceLastLog, 0);
            }
        }
    }
}
