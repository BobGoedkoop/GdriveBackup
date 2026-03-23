using System;
using System.Threading;

namespace GDriveBackup.Crosscutting.Logging
{
    public static class ConsoleOutputCoordinator
    {
        private static readonly object SyncRoot = new object();
        private static int _heartbeatActive;

        public static void SetHeartbeatActive(bool isActive)
        {
            Interlocked.Exchange(ref _heartbeatActive, isActive ? 1 : 0);
        }

        public static void WriteHeartbeat(string line)
        {
            lock (SyncRoot)
            {
                Console.Write(line);
            }
        }

        public static void ClearHeartbeatLine()
        {
            lock (SyncRoot)
            {
                Console.Write("\r");
            }
        }

        public static void PrepareForLogLine()
        {
            // Intentionally no-op: keep legacy behavior where logs may continue on heartbeat line.
        }
    }
}
