// =============================================================================
// AppConfig.cs
//
// Configuration settings for the COMOS Event Listener.
// Loaded from appsettings.json if present, otherwise uses defaults.
// =============================================================================

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ComosEventListener
{
    /// <summary>
    /// Configuration for the COMOS Event Listener application.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Output folder where event JSON files are written.
        /// Supports environment variables like %USERPROFILE%.
        /// </summary>
        public string OutputFolder { get; set; }

        /// <summary>
        /// Whether to capture "Before" (pre-DB-update) events.
        /// </summary>
        public bool CaptureBeforeEvents { get; set; } = true;

        /// <summary>
        /// Whether to capture "After" (post-DB-update) events.
        /// </summary>
        public bool CaptureAfterEvents { get; set; } = true;

        /// <summary>
        /// Whether to log to the console in real-time.
        /// </summary>
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>
        /// Whether to write a consolidated summary log file.
        /// </summary>
        public bool WriteSummaryLog { get; set; } = true;

        /// <summary>
        /// Maximum number of event files to keep (0 = unlimited).
        /// Old files are deleted when the limit is reached.
        /// </summary>
        public int MaxEventFiles { get; set; } = 0;

        /// <summary>
        /// COMOS ProgID override. Leave empty to use defaults.
        /// </summary>
        public string ComosProgId { get; set; } = "";

        /// <summary>
        /// SQL Server connection string for the database containing the
        /// comosad.EventFilter table. Leave empty to disable DB-based filtering
        /// (all events will be captured).
        /// </summary>
        public string EventFilterConnectionString { get; set; } = "";

        /// <summary>
        /// How often (in seconds) the event filter list is refreshed from the DB.
        /// Defaults to 60 seconds. Set to 0 to only load once at startup.
        /// </summary>
        public int EventFilterRefreshIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Loads configuration from appsettings.json if it exists next to the executable.
        /// </summary>
        public static AppConfig Load()
        {
            string configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "appsettings.json");

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<AppConfig>(json);
                    Console.WriteLine($"[Config] Loaded from {configPath}");
                    return config;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Config] Error reading config: {ex.Message}. Using defaults.");
                }
            }

            return new AppConfig
            {
                OutputFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "ComosEvents",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"))
            };
        }

        /// <summary>
        /// Generates a sample appsettings.json file.
        /// </summary>
        public static void GenerateSampleConfig(string outputPath)
        {
            var sample = new JObject
            {
                ["OutputFolder"] = @"C:\ComosEvents",
                ["CaptureBeforeEvents"] = true,
                ["CaptureAfterEvents"] = true,
                ["ConsoleLogging"] = true,
                ["WriteSummaryLog"] = true,
                ["MaxEventFiles"] = 0,
                ["ComosProgId"] = "",
                ["EventFilterConnectionString"] = "Server=YOUR_SERVER;Database=YOUR_DB;Integrated Security=True;",
                ["EventFilterRefreshIntervalSeconds"] = 60
            };

            File.WriteAllText(outputPath, sample.ToString(Formatting.Indented));
        }
    }
}
