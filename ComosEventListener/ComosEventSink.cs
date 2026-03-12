// =============================================================================
// ComosEventSink.cs
//
// Implements the COMOS event sink interface to capture ALL pre-DB-update events
// from the COMOS kernel. This class is registered as a COM-visible sink so that
// COMOS can call back into it when objects or attributes are about to change.
//
// Every event is serialized to JSON and written to the output folder via
// the EventLogger. The sink captures:
//   - Object writes (create/update/delete) BEFORE they hit the database
//   - Attribute/specification value changes with old and new values
//   - Object lifecycle events (create, delete)
// =============================================================================

using System;
using System.Runtime.InteropServices;
using ComosEventListener.ComosInterop;
using Newtonsoft.Json.Linq;

namespace ComosEventListener
{
    /// <summary>
    /// COM-visible event sink that COMOS calls for every database-related event.
    /// Implements IComosDEventSink to receive notifications before and after
    /// DB operations. All "Before" events fire BEFORE the database commit.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class ComosEventSink : IComosDEventSink
    {
        private readonly EventLogger _logger;
        private readonly EventFilterRepository _filter;

        /// <summary>
        /// Creates a new ComosEventSink.
        /// </summary>
        /// <param name="logger">Logger to write event JSON files.</param>
        /// <param name="filter">
        /// Optional filter repository. When provided, only events whose names
        /// appear in the comosad.EventFilter table (with IsActive = 1) are
        /// captured. Pass null to capture all events.
        /// </param>
        public ComosEventSink(EventLogger logger, EventFilterRepository filter = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _filter = filter;
        }

        /// <summary>
        /// Returns true if the event should be processed based on the DB filter.
        /// If no filter is configured, all events are allowed.
        /// </summary>
        private bool ShouldProcess(string eventType)
        {
            if (_filter == null)
                return true;
            return _filter.IsEventAllowed(eventType);
        }

        // =====================================================================
        // BEFORE OBJECT WRITE - Fires before any object update is saved to DB
        // =====================================================================
        public void OnBeforeObjectWrite(object comosObject)
        {
            if (!ShouldProcess("BeforeObjectWrite")) return;
            try
            {
                var data = ExtractObjectInfo(comosObject, "BeforeObjectWrite");
                data["phase"] = "PRE_DB_UPDATE";
                _logger.LogEvent("BeforeObjectWrite", data);
                _logger.AppendToSummaryLog("BeforeObjectWrite",
                    $"Object: {SafeGetProperty(comosObject, "SystemFullName")}");
            }
            catch (Exception ex)
            {
                LogError("OnBeforeObjectWrite", ex);
            }
        }

        // =====================================================================
        // AFTER OBJECT WRITE - Fires after an object update is committed to DB
        // =====================================================================
        public void OnAfterObjectWrite(object comosObject)
        {
            if (!ShouldProcess("AfterObjectWrite")) return;
            try
            {
                var data = ExtractObjectInfo(comosObject, "AfterObjectWrite");
                data["phase"] = "POST_DB_UPDATE";
                _logger.LogEvent("AfterObjectWrite", data);
                _logger.AppendToSummaryLog("AfterObjectWrite",
                    $"Object: {SafeGetProperty(comosObject, "SystemFullName")}");
            }
            catch (Exception ex)
            {
                LogError("OnAfterObjectWrite", ex);
            }
        }

        // =====================================================================
        // BEFORE OBJECT DELETE - Fires before an object is deleted from DB
        // =====================================================================
        public void OnBeforeObjectDelete(object comosObject)
        {
            if (!ShouldProcess("BeforeObjectDelete")) return;
            try
            {
                var data = ExtractObjectInfo(comosObject, "BeforeObjectDelete");
                data["phase"] = "PRE_DB_DELETE";
                _logger.LogEvent("BeforeObjectDelete", data);
                _logger.AppendToSummaryLog("BeforeObjectDelete",
                    $"Object: {SafeGetProperty(comosObject, "SystemFullName")}");
            }
            catch (Exception ex)
            {
                LogError("OnBeforeObjectDelete", ex);
            }
        }

        // =====================================================================
        // AFTER OBJECT DELETE - Fires after an object is deleted from DB
        // =====================================================================
        public void OnAfterObjectDelete(object comosObject)
        {
            if (!ShouldProcess("AfterObjectDelete")) return;
            try
            {
                var data = ExtractObjectInfo(comosObject, "AfterObjectDelete");
                data["phase"] = "POST_DB_DELETE";
                _logger.LogEvent("AfterObjectDelete", data);
                _logger.AppendToSummaryLog("AfterObjectDelete",
                    $"Object: {SafeGetProperty(comosObject, "SystemFullName")}");
            }
            catch (Exception ex)
            {
                LogError("OnAfterObjectDelete", ex);
            }
        }

        // =====================================================================
        // BEFORE ATTRIBUTE CHANGE - Fires before a specification/attribute
        //   value is committed. Captures both old and new values.
        // =====================================================================
        public void OnBeforeAttributeChange(object specification, object oldValue, object newValue)
        {
            if (!ShouldProcess("BeforeAttributeChange")) return;
            try
            {
                var data = new JObject
                {
                    ["eventCategory"] = "AttributeChange",
                    ["phase"] = "PRE_DB_UPDATE",
                    ["specification"] = ExtractSpecificationInfo(specification),
                    ["oldValue"] = oldValue?.ToString() ?? "(null)",
                    ["newValue"] = newValue?.ToString() ?? "(null)",
                    ["ownerObject"] = SafeGetProperty(specification, "Owner") != null
                        ? ExtractObjectInfo(GetComProperty(specification, "Owner"), "OwnerContext")
                        : new JObject { ["info"] = "Owner not available" }
                };

                _logger.LogEvent("BeforeAttributeChange", data);

                string specName = SafeGetProperty(specification, "Name");
                string ownerName = SafeGetProperty(
                    GetComProperty(specification, "Owner"), "SystemFullName");
                _logger.AppendToSummaryLog("BeforeAttributeChange",
                    $"Spec: {specName} on {ownerName} | '{oldValue}' -> '{newValue}'");
            }
            catch (Exception ex)
            {
                LogError("OnBeforeAttributeChange", ex);
            }
        }

        // =====================================================================
        // AFTER ATTRIBUTE CHANGE - Fires after a specification/attribute change
        // =====================================================================
        public void OnAfterAttributeChange(object specification)
        {
            if (!ShouldProcess("AfterAttributeChange")) return;
            try
            {
                var data = new JObject
                {
                    ["eventCategory"] = "AttributeChange",
                    ["phase"] = "POST_DB_UPDATE",
                    ["specification"] = ExtractSpecificationInfo(specification)
                };

                _logger.LogEvent("AfterAttributeChange", data);
                _logger.AppendToSummaryLog("AfterAttributeChange",
                    $"Spec: {SafeGetProperty(specification, "Name")}");
            }
            catch (Exception ex)
            {
                LogError("OnAfterAttributeChange", ex);
            }
        }

        // =====================================================================
        // BEFORE OBJECT CREATE - Fires before a new object is inserted into DB
        // =====================================================================
        public void OnBeforeObjectCreate(object comosObject)
        {
            if (!ShouldProcess("BeforeObjectCreate")) return;
            try
            {
                var data = ExtractObjectInfo(comosObject, "BeforeObjectCreate");
                data["phase"] = "PRE_DB_INSERT";
                _logger.LogEvent("BeforeObjectCreate", data);
                _logger.AppendToSummaryLog("BeforeObjectCreate",
                    $"Object: {SafeGetProperty(comosObject, "SystemFullName")}");
            }
            catch (Exception ex)
            {
                LogError("OnBeforeObjectCreate", ex);
            }
        }

        // =====================================================================
        // AFTER OBJECT CREATE - Fires after a new object is inserted into DB
        // =====================================================================
        public void OnAfterObjectCreate(object comosObject)
        {
            if (!ShouldProcess("AfterObjectCreate")) return;
            try
            {
                var data = ExtractObjectInfo(comosObject, "AfterObjectCreate");
                data["phase"] = "POST_DB_INSERT";
                _logger.LogEvent("AfterObjectCreate", data);
                _logger.AppendToSummaryLog("AfterObjectCreate",
                    $"Object: {SafeGetProperty(comosObject, "SystemFullName")}");
            }
            catch (Exception ex)
            {
                LogError("OnAfterObjectCreate", ex);
            }
        }

        // =====================================================================
        // Helper: Extract all available info from a COMOS object via COM
        // =====================================================================
        private JObject ExtractObjectInfo(object comosObject, string context)
        {
            var info = new JObject
            {
                ["eventCategory"] = "ObjectEvent",
                ["context"] = context,
                ["systemFullName"] = SafeGetProperty(comosObject, "SystemFullName"),
                ["name"] = SafeGetProperty(comosObject, "Name"),
                ["label"] = SafeGetProperty(comosObject, "Label"),
                ["description"] = SafeGetProperty(comosObject, "Description"),
                ["systemType"] = SafeGetProperty(comosObject, "SystemType"),
                ["uid"] = SafeGetProperty(comosObject, "UID"),
                ["objectType"] = comosObject?.GetType()?.FullName ?? "Unknown"
            };

            // Try to get the owner hierarchy
            try
            {
                object owner = GetComProperty(comosObject, "Owner");
                if (owner != null)
                {
                    info["ownerName"] = SafeGetProperty(owner, "Name");
                    info["ownerSystemFullName"] = SafeGetProperty(owner, "SystemFullName");
                }
            }
            catch
            {
                // Owner not available for this object type
            }

            return info;
        }

        // =====================================================================
        // Helper: Extract specification/attribute info
        // =====================================================================
        private JObject ExtractSpecificationInfo(object specification)
        {
            return new JObject
            {
                ["name"] = SafeGetProperty(specification, "Name"),
                ["label"] = SafeGetProperty(specification, "Label"),
                ["description"] = SafeGetProperty(specification, "Description"),
                ["value"] = SafeGetProperty(specification, "Value"),
                ["displayValue"] = SafeGetProperty(specification, "DisplayValue"),
                ["unit"] = SafeGetProperty(specification, "Unit"),
                ["systemFullName"] = SafeGetProperty(specification, "SystemFullName")
            };
        }

        // =====================================================================
        // Helper: Safely read a COM property via late binding (IDispatch)
        // =====================================================================
        private string SafeGetProperty(object comObject, string propertyName)
        {
            if (comObject == null) return "(null object)";
            try
            {
                object value = comObject.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    comObject,
                    null);
                return value?.ToString() ?? "(null)";
            }
            catch
            {
                return $"(unable to read '{propertyName}')";
            }
        }

        // =====================================================================
        // Helper: Get a COM property as an object (for further inspection)
        // =====================================================================
        private object GetComProperty(object comObject, string propertyName)
        {
            if (comObject == null) return null;
            try
            {
                return comObject.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    comObject,
                    null);
            }
            catch
            {
                return null;
            }
        }

        // =====================================================================
        // Helper: Log errors
        // =====================================================================
        private void LogError(string methodName, Exception ex)
        {
            var errorData = new JObject
            {
                ["errorInMethod"] = methodName,
                ["exceptionType"] = ex.GetType().FullName,
                ["message"] = ex.Message,
                ["stackTrace"] = ex.StackTrace
            };
            _logger.LogEvent("ERROR", errorData);
            Console.Error.WriteLine(
                $"[ComosEventListener ERROR] {methodName}: {ex.Message}");
        }
    }
}
