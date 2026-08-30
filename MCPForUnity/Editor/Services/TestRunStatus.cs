using System;
using UnityEditor.TestTools.TestRunner.Api;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Thread-safe, minimal shared status for Unity Test Runner execution.
    /// Used by editor readiness snapshots so callers can avoid starting overlapping runs.
    /// </summary>
    internal static class TestRunStatus
    {
        private static readonly object LockObj = new();

        private static bool _isRunning;
        private static TestMode? _mode;
        private static long? _startedUnixMs;
        private static long? _finishedUnixMs;
        private static string _startedBy;

        public static bool IsRunning
        {
            get { lock (LockObj) return _isRunning; }
        }

        public static TestMode? Mode
        {
            get { lock (LockObj) return _mode; }
        }

        public static long? StartedUnixMs
        {
            get { lock (LockObj) return _startedUnixMs; }
        }

        public static long? FinishedUnixMs
        {
            get { lock (LockObj) return _finishedUnixMs; }
        }

        /// <summary>
        /// Label of the MCP client that started the current run, or null when
        /// it was started by the human in the Test Runner window or by a client
        /// the server could not identify.
        /// </summary>
        public static string StartedBy
        {
            get { lock (LockObj) return _startedBy; }
        }

        public static void MarkStarted(TestMode mode, string startedBy = null)
        {
            lock (LockObj)
            {
                _isRunning = true;
                _mode = mode;
                _startedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _finishedUnixMs = null;
                _startedBy = startedBy;
            }
        }

        public static void MarkFinished()
        {
            lock (LockObj)
            {
                _isRunning = false;
                _finishedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _mode = null;
                _startedBy = null;
            }
        }
    }
}


