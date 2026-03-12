// =============================================================================
// EventLogger.cs
//
// Thread-safe file logger that writes COMOS event data as JSON files
// to a configurable output folder. Each event produces a separate JSON file
// with a timestamped filename for easy chronological tracking.
// =============================================================================

using System;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ComosEventListener
{
    /// <summary>
    /// Thread-safe logger that writes COMOS event data to JSON files in a target folder.
    /// </summary>
    public class EventLogger : IDisposable
    {
        private readonly string _outputFolder;
        private readonly object _fileLock = new object();
        private readonly string _sessionId;
        private long _eventCounter;
        private bool _disposed;

        /// <summary>
        /// Creates a new EventLogger that writes to the specified folder.
        /// </summary>
        /// <param name="outputFolder">
        /// Full path to the output folder. Created automatically if it does not exist.
        /// </param>
        public EventLogger(string outputFolder)
        {
            _outputFolder = outputFolder ?? throw new ArgumentNullException(nameof(outputFolder));
            _sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            _eventCounter = 0;

            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            // Write a session start marker file
            var sessionInfo = new JObject
            {
                ["sessionId"] = _sessionId,
                ["startTimeUtc"] = DateTime.UtcNow.ToString("o"),
                ["machineName"] = Environment.MachineName,
                ["userName"] = Environment.UserName,
                ["outputFolder"] = _outputFolder
            };

            WriteJsonFile("_session_start", sessionInfo);
            LogToConsole("EventLogger initialized. Output folder: " + _outputFolder);
        }

        /// <summary>
        /// Logs a COMOS event to a JSON file.
        /// </summary>
        /// <param name="eventType">The type/category of the event (e.g., "BeforeObjectWrite").</param>
        /// <param name="eventData">A JObject containing all event details.</param>
        public void LogEvent(string eventType, JObject eventData)
        {
            if (_disposed) return;

            long counter = Interlocked.Increment(ref _eventCounter);

            var envelope = new JObject
            {
                ["eventId"] = counter,
                ["sessionId"] = _sessionId,
                ["timestampUtc"] = DateTime.UtcNow.ToString("o"),
                ["eventType"] = eventType,
                ["data"] = eventData
            };

            string filePrefix = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{counter:D6}_{eventType}";
            WriteJsonFile(filePrefix, envelope);
            LogToConsole($"[Event #{counter}] {eventType}");
        }

        /// <summary>
        /// Appends a summary line to a consolidated log file (one line per event).
        /// This is in addition to the individual JSON files, for quick scanning.
        /// </summary>
        /// <param name="eventType">The event type.</param>
        /// <param name="summary">A one-line summary string.</param>
        public void AppendToSummaryLog(string eventType, string summary)
        {
            if (_disposed) return;

            string logFile = Path.Combine(_outputFolder, $"_event_summary_{_sessionId}.log");
            string line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} | {eventType,-30} | {summary}";

            lock (_fileLock)
            {
                File.AppendAllText(logFile, line + Environment.NewLine, Encoding.UTF8);
            }
        }

        private void WriteJsonFile(string filePrefix, JObject content)
        {
            // Sanitize file prefix
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                filePrefix = filePrefix.Replace(c, '_');
            }

            string filePath = Path.Combine(_outputFolder, filePrefix + ".json");

            lock (_fileLock)
            {
                File.WriteAllText(filePath, content.ToString(Formatting.Indented), Encoding.UTF8);
            }
        }

        private void LogToConsole(string message)
        {
            Console.WriteLine($"[ComosEventListener] {DateTime.UtcNow:HH:mm:ss.fff} {message}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                var sessionEnd = new JObject
                {
                    ["sessionId"] = _sessionId,
                    ["endTimeUtc"] = DateTime.UtcNow.ToString("o"),
                    ["totalEventsLogged"] = _eventCounter
                };
                WriteJsonFile("_session_end", sessionEnd);
                LogToConsole($"EventLogger disposed. Total events logged: {_eventCounter}");
            }
        }
    }
}
