// =============================================================================
// Program.cs
//
// Console application entry point for the COMOS 10.5.2 Event Listener Add-In.
//
// This program:
//   1. Connects to a running COMOS 10.5.2 instance via COM automation
//   2. Registers an event sink to listen to ALL pre-DB-update events
//   3. Logs every captured event as a JSON file to a configurable output folder
//   4. Runs until the user presses a key or COMOS disconnects
//
// Usage:
//   ComosEventListener.exe [output_folder]
//
// If no output folder is specified, defaults to:
//   %USERPROFILE%\ComosEvents\<timestamp>\
//
// Requirements:
//   - COMOS 10.5.2 must be installed on the machine
//   - A COMOS project must be open with an active workset
//   - The application must run on the same machine as the COMOS client
//   - Run as the same Windows user that launched COMOS
// =============================================================================

using System;
using System.IO;
using System.Threading;

namespace ComosEventListener
{
    class Program
    {
        [STAThread] // Required for COM STA (Single-Threaded Apartment) model
        static void Main(string[] args)
        {
            Console.Title = "COMOS 10.5.2 Event Listener";
            PrintBanner();

            // Determine output folder
            string outputFolder = args.Length > 0
                ? args[0]
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "ComosEvents",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            Console.WriteLine($"Output folder: {outputFolder}");
            Console.WriteLine();

            EventLogger logger = null;
            ComosConnectionManager connectionManager = null;

            try
            {
                // Initialize the event logger
                logger = new EventLogger(outputFolder);

                // Create the event sink
                var eventSink = new ComosEventSink(logger);

                // Connect to COMOS and register the event sink
                connectionManager = new ComosConnectionManager();
                bool connected = connectionManager.Connect(eventSink);

                if (!connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine();
                    Console.WriteLine("========================================================");
                    Console.WriteLine(" FAILED TO CONNECT TO COMOS");
                    Console.WriteLine("========================================================");
                    Console.WriteLine();
                    Console.WriteLine("Please ensure:");
                    Console.WriteLine("  1. COMOS 10.5.2 is installed on this machine");
                    Console.WriteLine("  2. COMOS is currently running");
                    Console.WriteLine("  3. A project is open with an active workset");
                    Console.WriteLine("  4. You are running this as the same Windows user");
                    Console.WriteLine("  5. This application is running as x86 (32-bit)");
                    Console.WriteLine();
                    Console.WriteLine("If COMOS is installed but not detected, verify the");
                    Console.WriteLine("COM ProgID registration:");
                    Console.WriteLine("  - COMOS.Application");
                    Console.WriteLine("  - Comos.Kernel.Application");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey(true);
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine("========================================================");
                Console.WriteLine(" COMOS EVENT LISTENER ACTIVE");
                Console.WriteLine("========================================================");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Listening for ALL events (pre-DB-update and post-update).");
                Console.WriteLine("Events are being written to:");
                Console.WriteLine($"  {outputFolder}");
                Console.WriteLine();
                Console.WriteLine("Event types being captured:");
                Console.WriteLine("  - BeforeObjectWrite    (object update, PRE-DB)");
                Console.WriteLine("  - AfterObjectWrite     (object update, POST-DB)");
                Console.WriteLine("  - BeforeObjectDelete   (object deletion, PRE-DB)");
                Console.WriteLine("  - AfterObjectDelete    (object deletion, POST-DB)");
                Console.WriteLine("  - BeforeAttributeChange (attribute change, PRE-DB)");
                Console.WriteLine("  - AfterAttributeChange  (attribute change, POST-DB)");
                Console.WriteLine("  - BeforeObjectCreate   (object creation, PRE-DB)");
                Console.WriteLine("  - AfterObjectCreate    (object creation, POST-DB)");
                Console.WriteLine();
                Console.WriteLine("Press 'Q' to quit, 'S' for status, 'O' to open output folder.");
                Console.WriteLine();

                // Main message pump loop - required for COM events in STA
                RunMessagePump(connectionManager, outputFolder);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nFATAL ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
            finally
            {
                // Clean up
                connectionManager?.Dispose();
                logger?.Dispose();

                Console.WriteLine();
                Console.WriteLine("COMOS Event Listener stopped.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
        }

        /// <summary>
        /// Runs a Windows message pump to keep COM events flowing on the STA thread.
        /// This is essential - without a message pump, COM events will not be dispatched
        /// to the event sink in an STA thread.
        /// </summary>
        private static void RunMessagePump(ComosConnectionManager connectionManager, string outputFolder)
        {
            bool running = true;

            while (running && connectionManager.IsConnected)
            {
                // Process COM messages (required for STA event dispatching)
                // This pumps the Windows message queue so COM callbacks can arrive
                Thread.CurrentThread.Join(100); // Yield for 100ms to allow COM callbacks

                // Check for user input (non-blocking)
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    switch (char.ToUpper(key.KeyChar))
                    {
                        case 'Q':
                            Console.WriteLine("\nShutting down event listener...");
                            running = false;
                            break;

                        case 'S':
                            Console.WriteLine($"\n[Status] Connected: {connectionManager.IsConnected}");
                            Console.WriteLine($"[Status] Output folder: {outputFolder}");
                            int fileCount = 0;
                            try
                            {
                                fileCount = Directory.GetFiles(outputFolder, "*.json").Length;
                            }
                            catch { /* folder may not exist yet */ }
                            Console.WriteLine($"[Status] Event files written: {fileCount}");
                            Console.WriteLine();
                            break;

                        case 'O':
                            try
                            {
                                System.Diagnostics.Process.Start("explorer.exe", outputFolder);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Could not open folder: {ex.Message}");
                            }
                            break;
                    }
                }
            }

            if (!connectionManager.IsConnected)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nCOMOS connection lost. The COMOS application may have been closed.");
                Console.ResetColor();
            }
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
  ================================================================
   COMOS 10.5.2 Event Listener Add-In
   Captures ALL pre-DB-update events and logs to folder as JSON
  ================================================================
");
            Console.ResetColor();
        }
    }
}
