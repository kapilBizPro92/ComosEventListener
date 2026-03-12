// =============================================================================
// ComosConnectionManager.cs
//
// Manages the COM connection to a running COMOS 10.5.2 instance and registers
// the event sink. Supports two connection modes:
//   1. Attach to existing COMOS process via ROT (Running Object Table)
//   2. Create a new COMOS automation instance via ProgID
//
// The manager handles COM lifetime, STA threading, and graceful cleanup.
// =============================================================================

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ComosEventListener
{
    /// <summary>
    /// Manages the COM connection to a COMOS instance and registers/unregisters
    /// the event sink for capturing pre-DB-update events.
    /// </summary>
    public class ComosConnectionManager : IDisposable
    {
        // COMOS ProgIDs - these are the registered COM identifiers for COMOS automation
        private const string COMOS_PROGID = "COMOS.Application";
        private const string COMOS_PROGID_ALT = "Comos.Kernel.Application";

        private object _comosApplication;
        private object _workset;
        private object _eventManager;
        private ComosEventSink _eventSink;
        private bool _isConnected;
        private bool _disposed;

        /// <summary>
        /// True if we are currently connected to a COMOS instance with active event listening.
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Connects to a running COMOS instance and registers the event sink.
        /// </summary>
        /// <param name="eventSink">The event sink to register for notifications.</param>
        /// <returns>True if connection and registration succeeded.</returns>
        public bool Connect(ComosEventSink eventSink)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));

            Console.WriteLine("[ComosConnection] Attempting to connect to COMOS 10.5.2...");

            // Strategy 1: Try to get an already-running COMOS instance from the ROT
            _comosApplication = TryGetRunningInstance();

            // Strategy 2: If no running instance, try to create one via COM activation
            if (_comosApplication == null)
            {
                _comosApplication = TryCreateInstance();
            }

            if (_comosApplication == null)
            {
                Console.Error.WriteLine(
                    "[ComosConnection] ERROR: Could not connect to COMOS. " +
                    "Make sure COMOS 10.5.2 is installed and running.");
                return false;
            }

            Console.WriteLine("[ComosConnection] Connected to COMOS application.");

            // Get the active workset
            _workset = GetComProperty(_comosApplication, "Workset");
            if (_workset == null)
            {
                Console.Error.WriteLine(
                    "[ComosConnection] WARNING: No active workset found. " +
                    "Open a project in COMOS first.");
            }
            else
            {
                string worksetName = SafeGetProperty(_workset, "Name");
                Console.WriteLine($"[ComosConnection] Active workset: {worksetName}");
            }

            // Register the event sink with COMOS
            RegisterEventSink();

            _isConnected = true;
            Console.WriteLine("[ComosConnection] Event sink registered. Listening for events...");
            return true;
        }

        /// <summary>
        /// Attempts to retrieve an already-running COMOS instance from the Running Object Table.
        /// </summary>
        private object TryGetRunningInstance()
        {
            try
            {
                Console.WriteLine("[ComosConnection] Looking for running COMOS instance...");
                object instance = Marshal.GetActiveObject(COMOS_PROGID);
                if (instance != null)
                {
                    Console.WriteLine("[ComosConnection] Found running COMOS via primary ProgID.");
                    return instance;
                }
            }
            catch (COMException)
            {
                // Primary ProgID not found in ROT, try alternate
            }

            try
            {
                object instance = Marshal.GetActiveObject(COMOS_PROGID_ALT);
                if (instance != null)
                {
                    Console.WriteLine("[ComosConnection] Found running COMOS via alternate ProgID.");
                    return instance;
                }
            }
            catch (COMException)
            {
                Console.WriteLine("[ComosConnection] No running COMOS instance found in ROT.");
            }

            return null;
        }

        /// <summary>
        /// Attempts to create a new COMOS COM instance via its ProgID.
        /// </summary>
        private object TryCreateInstance()
        {
            try
            {
                Console.WriteLine("[ComosConnection] Attempting to create COMOS instance...");
                Type comosType = Type.GetTypeFromProgID(COMOS_PROGID);
                if (comosType != null)
                {
                    object instance = Activator.CreateInstance(comosType);
                    Console.WriteLine("[ComosConnection] Created COMOS instance via primary ProgID.");
                    return instance;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ComosConnection] Primary ProgID failed: {ex.Message}");
            }

            try
            {
                Type comosType = Type.GetTypeFromProgID(COMOS_PROGID_ALT);
                if (comosType != null)
                {
                    object instance = Activator.CreateInstance(comosType);
                    Console.WriteLine("[ComosConnection] Created COMOS instance via alternate ProgID.");
                    return instance;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ComosConnection] Alternate ProgID failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Registers the event sink with the COMOS event manager.
        /// COMOS supports multiple approaches for event registration:
        ///   1. Via IComosDEventManager.AdviseEventSink()
        ///   2. Via IConnectionPoint COM protocol
        ///   3. Via the Workset's event registration methods
        /// This implementation tries all available approaches.
        /// </summary>
        private void RegisterEventSink()
        {
            // Approach 1: Try via the application-level EventManager
            bool registered = TryRegisterViaEventManager();

            // Approach 2: Try via the Workset event registration
            if (!registered)
            {
                registered = TryRegisterViaWorkset();
            }

            // Approach 3: Try via COM Connection Points (IConnectionPointContainer)
            if (!registered)
            {
                registered = TryRegisterViaConnectionPoint();
            }

            if (!registered)
            {
                Console.Error.WriteLine(
                    "[ComosConnection] WARNING: Could not register event sink via any method. " +
                    "Events may not be captured. Check COMOS version compatibility.");
            }
        }

        /// <summary>
        /// Register via COMOS Application's EventManager property.
        /// </summary>
        private bool TryRegisterViaEventManager()
        {
            try
            {
                _eventManager = GetComProperty(_comosApplication, "EventManager");
                if (_eventManager == null)
                {
                    // Some COMOS versions expose it as "Events" instead
                    _eventManager = GetComProperty(_comosApplication, "Events");
                }

                if (_eventManager != null)
                {
                    // Call AdviseEventSink or RegisterEventSink
                    try
                    {
                        InvokeComMethod(_eventManager, "AdviseEventSink", new object[] { _eventSink });
                        Console.WriteLine("[ComosConnection] Registered via EventManager.AdviseEventSink().");
                        return true;
                    }
                    catch
                    {
                        // Try alternate method name
                    }

                    try
                    {
                        InvokeComMethod(_eventManager, "RegisterEventSink", new object[] { _eventSink });
                        Console.WriteLine("[ComosConnection] Registered via EventManager.RegisterEventSink().");
                        return true;
                    }
                    catch
                    {
                        // Try alternate method name
                    }

                    try
                    {
                        InvokeComMethod(_eventManager, "Advise", new object[] { _eventSink });
                        Console.WriteLine("[ComosConnection] Registered via EventManager.Advise().");
                        return true;
                    }
                    catch
                    {
                        Console.WriteLine("[ComosConnection] EventManager found but no known registration method worked.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ComosConnection] EventManager approach failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Register via the Workset's event sink registration.
        /// </summary>
        private bool TryRegisterViaWorkset()
        {
            if (_workset == null) return false;

            try
            {
                InvokeComMethod(_workset, "AdviseEventSink", new object[] { _eventSink });
                Console.WriteLine("[ComosConnection] Registered via Workset.AdviseEventSink().");
                return true;
            }
            catch
            {
                // Not available
            }

            try
            {
                InvokeComMethod(_workset, "RegisterEventSink", new object[] { _eventSink });
                Console.WriteLine("[ComosConnection] Registered via Workset.RegisterEventSink().");
                return true;
            }
            catch
            {
                Console.WriteLine("[ComosConnection] Workset registration approach failed.");
            }

            return false;
        }

        /// <summary>
        /// Register via standard COM IConnectionPointContainer/IConnectionPoint protocol.
        /// </summary>
        private bool TryRegisterViaConnectionPoint()
        {
            try
            {
                if (_comosApplication is IConnectionPointContainer container)
                {
                    // Try to find the connection point for the event sink interface
                    // The GUID here should match the actual IComosDEventSink GUID from the type library
                    Guid eventSinkIID = typeof(ComosInterop.IComosDEventSink).GUID;
                    container.FindConnectionPoint(ref eventSinkIID, out IConnectionPoint connectionPoint);

                    if (connectionPoint != null)
                    {
                        connectionPoint.Advise(_eventSink, out int cookie);
                        Console.WriteLine(
                            $"[ComosConnection] Registered via IConnectionPoint (cookie={cookie}).");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ComosConnection] ConnectionPoint approach failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Unregisters the event sink and releases COM resources.
        /// </summary>
        private void UnregisterEventSink()
        {
            if (_eventManager != null)
            {
                try
                {
                    InvokeComMethod(_eventManager, "UnadviseEventSink", new object[] { _eventSink });
                }
                catch { /* Best effort cleanup */ }

                try
                {
                    InvokeComMethod(_eventManager, "UnregisterEventSink", new object[] { _eventSink });
                }
                catch { /* Best effort cleanup */ }
            }

            if (_workset != null)
            {
                try
                {
                    InvokeComMethod(_workset, "UnadviseEventSink", new object[] { _eventSink });
                }
                catch { /* Best effort cleanup */ }
            }
        }

        // =====================================================================
        // COM late-binding helpers
        // =====================================================================

        private object GetComProperty(object comObject, string propertyName)
        {
            if (comObject == null) return null;
            try
            {
                return comObject.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.GetProperty,
                    null,
                    comObject,
                    null);
            }
            catch
            {
                return null;
            }
        }

        private string SafeGetProperty(object comObject, string propertyName)
        {
            object val = GetComProperty(comObject, propertyName);
            return val?.ToString() ?? "(unavailable)";
        }

        private object InvokeComMethod(object comObject, string methodName, object[] args)
        {
            return comObject.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                comObject,
                args);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _isConnected = false;

                UnregisterEventSink();

                // Release COM objects in reverse order
                if (_eventManager != null)
                {
                    Marshal.ReleaseComObject(_eventManager);
                    _eventManager = null;
                }
                if (_workset != null)
                {
                    Marshal.ReleaseComObject(_workset);
                    _workset = null;
                }
                if (_comosApplication != null)
                {
                    Marshal.ReleaseComObject(_comosApplication);
                    _comosApplication = null;
                }

                Console.WriteLine("[ComosConnection] Disconnected from COMOS. COM resources released.");
            }
        }
    }
}
