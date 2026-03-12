// =============================================================================
// EventFilterRepository.cs
//
// Reads allowed event names from the [comosad].[EventFilter] database table.
// Only events whose names appear in this table will be captured and logged.
//
// Expected table schema:
//   CREATE TABLE [comosad].[EventFilter] (
//       [Id]        INT IDENTITY(1,1) PRIMARY KEY,
//       [EventName] NVARCHAR(255) NOT NULL,
//       [IsActive]  BIT NOT NULL DEFAULT 1
//   );
//
// Example rows:
//   INSERT INTO comosad.EventFilter (EventName, IsActive) VALUES ('BeforeObjectWrite', 1);
//   INSERT INTO comosad.EventFilter (EventName, IsActive) VALUES ('BeforeAttributeChange', 1);
//   INSERT INTO comosad.EventFilter (EventName, IsActive) VALUES ('AfterObjectCreate', 0);
//
// Only rows where IsActive = 1 are loaded. If the table is empty or
// unreachable, ALL events are captured (fail-open behavior).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace ComosEventListener
{
    /// <summary>
    /// Reads event filter names from the comosad.EventFilter database table
    /// and provides a fast lookup to determine whether an event should be processed.
    /// </summary>
    public class EventFilterRepository
    {
        private readonly string _connectionString;
        private HashSet<string> _allowedEvents;
        private readonly object _lock = new object();
        private DateTime _lastRefresh;
        private readonly TimeSpan _refreshInterval;

        /// <summary>
        /// Creates a new EventFilterRepository.
        /// </summary>
        /// <param name="connectionString">
        /// SQL Server connection string pointing to the database containing
        /// the comosad.EventFilter table.
        /// </param>
        /// <param name="refreshIntervalSeconds">
        /// How often (in seconds) to re-read the filter table from the DB.
        /// Defaults to 60 seconds. Set to 0 to disable auto-refresh.
        /// </param>
        public EventFilterRepository(string connectionString, int refreshIntervalSeconds = 60)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _refreshInterval = TimeSpan.FromSeconds(refreshIntervalSeconds);
            _lastRefresh = DateTime.MinValue;
            _allowedEvents = null;
        }

        /// <summary>
        /// Returns true if the given event type should be captured.
        /// Performs a case-insensitive comparison against the EventName values
        /// in the comosad.EventFilter table.
        /// If the filter table is empty or cannot be read, returns true (fail-open).
        /// </summary>
        public bool IsEventAllowed(string eventType)
        {
            EnsureFiltersLoaded();

            lock (_lock)
            {
                // If no filters loaded (empty table or error), allow everything
                if (_allowedEvents == null || _allowedEvents.Count == 0)
                    return true;

                return _allowedEvents.Contains(eventType);
            }
        }

        /// <summary>
        /// Forces an immediate reload of the filter list from the database.
        /// </summary>
        public void Refresh()
        {
            LoadFiltersFromDatabase();
        }

        /// <summary>
        /// Returns the current set of allowed event names (for status display).
        /// </summary>
        public IReadOnlyCollection<string> GetAllowedEvents()
        {
            EnsureFiltersLoaded();

            lock (_lock)
            {
                if (_allowedEvents == null)
                    return Array.Empty<string>();

                return _allowedEvents.ToList().AsReadOnly();
            }
        }

        private void EnsureFiltersLoaded()
        {
            bool needsRefresh;
            lock (_lock)
            {
                needsRefresh = _allowedEvents == null
                    || (_refreshInterval > TimeSpan.Zero
                        && DateTime.UtcNow - _lastRefresh > _refreshInterval);
            }

            if (needsRefresh)
            {
                LoadFiltersFromDatabase();
            }
        }

        private void LoadFiltersFromDatabase()
        {
            try
            {
                var events = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(
                        "SELECT [EventName] FROM [comosad].[EventFilter] WHERE [IsActive] = 1",
                        connection))
                    {
                        command.CommandTimeout = 10;

                        using (IDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string eventName = reader.GetString(0)?.Trim();
                                if (!string.IsNullOrEmpty(eventName))
                                {
                                    events.Add(eventName);
                                }
                            }
                        }
                    }
                }

                lock (_lock)
                {
                    _allowedEvents = events;
                    _lastRefresh = DateTime.UtcNow;
                }

                Console.WriteLine(
                    $"[EventFilter] Loaded {events.Count} active filter(s) from comosad.EventFilter: " +
                    $"{string.Join(", ", events)}");

                if (events.Count == 0)
                {
                    Console.WriteLine(
                        "[EventFilter] No active filters found. ALL events will be captured.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[EventFilter] ERROR reading comosad.EventFilter: {ex.Message}");
                Console.Error.WriteLine(
                    "[EventFilter] Falling back to capturing ALL events.");

                lock (_lock)
                {
                    // On error, keep existing filters if we have them;
                    // otherwise set to empty (which means allow-all)
                    if (_allowedEvents == null)
                        _allowedEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    _lastRefresh = DateTime.UtcNow;
                }
            }
        }
    }
}
