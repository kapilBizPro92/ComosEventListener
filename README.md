# COMOS 10.5.2 Event Listener Add-In

A C# console application that connects to a running Siemens COMOS 10.5.2 instance via COM automation, listens to **all events published before DB updates**, and logs them as JSON files to a configurable output folder.

## What It Captures

| Event | Phase | Description |
|-------|-------|-------------|
| `BeforeObjectWrite` | **PRE-DB** | Object is about to be updated in the database |
| `AfterObjectWrite` | POST-DB | Object update has been committed |
| `BeforeObjectDelete` | **PRE-DB** | Object is about to be deleted |
| `AfterObjectDelete` | POST-DB | Object deletion committed |
| `BeforeAttributeChange` | **PRE-DB** | Attribute/specification value changing (captures old + new values) |
| `AfterAttributeChange` | POST-DB | Attribute change committed |
| `BeforeObjectCreate` | **PRE-DB** | New object about to be inserted |
| `AfterObjectCreate` | POST-DB | New object creation committed |

All **"Before"** events fire **before the database commit**, giving you a snapshot of what is about to change.

## Prerequisites

- **COMOS 10.5.2** installed on the machine
- **Visual Studio 2019/2022** with .NET Framework 4.8 targeting pack
- COMOS must be **running** with a **project open** and an **active workset**
- Run the listener on the **same machine** as the COMOS client
- Run as the **same Windows user** that launched COMOS

## Project Structure

```
ComosEventListener/
├── ComosEventListener.sln          # Visual Studio solution
├── appsettings.json                # Sample configuration file
├── README.md                       # This file
└── ComosEventListener/
    ├── ComosEventListener.csproj   # Project file (.NET Framework 4.8, x86)
    ├── Program.cs                  # Console entry point with message pump
    ├── ComosInterfaces.cs          # COM interface definitions (placeholder GUIDs)
    ├── ComosEventSink.cs           # Event sink implementation (captures all events)
    ├── ComosConnectionManager.cs   # COM connection + event registration logic
    ├── EventLogger.cs              # Thread-safe JSON file logger
    ├── EventFilterRepository.cs    # DB-based event filtering (comosad.EventFilter)
    └── AppConfig.cs                # Configuration loader
```

## Setup Instructions

### Step 1: Add COMOS COM References

The `ComosInterfaces.cs` file contains **placeholder interface definitions** with dummy GUIDs. Once COMOS is installed, you need to replace these with the actual COMOS type library references.

**Option A: Visual Studio COM Reference (Recommended)**

1. Open the solution in Visual Studio
2. Right-click the project → **Add Reference** → **COM** → **Type Libraries**
3. Search for "COMOS" and add the relevant type libraries:
   - `COMOS Kernel Type Library`
   - `COMOS Global Type Library`
4. After adding the COM references, you can **delete `ComosInterfaces.cs`** and update the `using` statements to use the generated interop namespaces

**Option B: Manual tlbimp**

```batch
tlbimp "C:\Program Files (x86)\Siemens\COMOS\Bin\ComosKernel.dll" /out:Interop.ComosKernel.dll
tlbimp "C:\Program Files (x86)\Siemens\COMOS\Bin\ComosGlobal.dll" /out:Interop.ComosGlobal.dll
```

Then add these as assembly references in the `.csproj`.

### Step 2: Update Interface GUIDs

If keeping the manual interface definitions in `ComosInterfaces.cs`:

1. Open the COMOS type library in **OLE/COM Object Viewer** (`oleview.exe`)
2. Find the actual GUIDs for each interface:
   - `IComosDDevice`
   - `IComosDSpecification`
   - `IComosDWorkset`
   - `IComosDProject`
   - `IComosDEventManager`
   - `IComosDEventSink`
3. Replace the placeholder GUIDs (`00000000-...`) in `ComosInterfaces.cs`

### Step 3: Build

```batch
msbuild ComosEventListener.sln /p:Configuration=Release /p:Platform="Any CPU"
```

Or build from Visual Studio (Release, Any CPU).

### Step 4: Configure (Optional)

Copy `appsettings.json` next to the built `.exe`:

```json
{
  "OutputFolder": "C:\\ComosEvents",
  "CaptureBeforeEvents": true,
  "CaptureAfterEvents": true,
  "ConsoleLogging": true,
  "WriteSummaryLog": true,
  "MaxEventFiles": 0,
  "ComosProgId": "",
  "EventFilterConnectionString": "Server=YOUR_SERVER;Database=YOUR_DB;Integrated Security=True;",
  "EventFilterRefreshIntervalSeconds": 60
}
```

## Usage

### Run with Default Settings

```batch
ComosEventListener.exe
```

Events are written to `%USERPROFILE%\ComosEvents\<timestamp>\`.

### Run with Custom Output Folder

```batch
ComosEventListener.exe "D:\MyProject\EventLogs"
```

### Interactive Commands

While running, press:
- **Q** - Quit the listener
- **S** - Show status (connection state, event count)
- **O** - Open the output folder in Explorer

## Output Format

Each event creates a separate JSON file:

```
ComosEvents/
├── _session_start.json
├── 20260312_143022_001_000001_BeforeAttributeChange.json
├── 20260312_143022_002_000002_AfterAttributeChange.json
├── 20260312_143025_003_000003_BeforeObjectWrite.json
├── _event_summary_20260312_143020.log
└── _session_end.json
```

### Sample Event JSON

```json
{
  "eventId": 1,
  "sessionId": "20260312_143020",
  "timestampUtc": "2026-03-12T14:30:22.456Z",
  "eventType": "BeforeAttributeChange",
  "data": {
    "eventCategory": "AttributeChange",
    "phase": "PRE_DB_UPDATE",
    "specification": {
      "name": "Temperature",
      "label": "Design Temperature",
      "description": "Operating temperature",
      "value": "150",
      "displayValue": "150 °C",
      "unit": "°C",
      "systemFullName": "@50|M001|EA001.Temperature"
    },
    "oldValue": "120",
    "newValue": "150",
    "ownerObject": {
      "eventCategory": "ObjectEvent",
      "context": "OwnerContext",
      "systemFullName": "@50|M001|EA001",
      "name": "EA001",
      "label": "Heat Exchanger 001",
      "description": "Shell and tube heat exchanger",
      "systemType": "Device",
      "uid": "abc123-def456"
    }
  }
}
```

### Summary Log Format

The `_event_summary_*.log` file contains one line per event for quick scanning:

```
2026-03-12 14:30:22.456 | BeforeAttributeChange        | Spec: Temperature on @50|M001|EA001 | '120' -> '150'
2026-03-12 14:30:25.789 | BeforeObjectWrite             | Object: @50|M001|EA001
```

## Event Filtering (comosad.EventFilter)

You can filter which events are captured by populating the `comosad.EventFilter` database table.

### Table Schema

```sql
CREATE TABLE [comosad].[EventFilter] (
    [Id]        INT IDENTITY(1,1) PRIMARY KEY,
    [EventName] NVARCHAR(255) NOT NULL,
    [IsActive]  BIT NOT NULL DEFAULT 1
);
```

### Example: Only capture pre-DB attribute and object write events

```sql
INSERT INTO comosad.EventFilter (EventName, IsActive) VALUES ('BeforeAttributeChange', 1);
INSERT INTO comosad.EventFilter (EventName, IsActive) VALUES ('BeforeObjectWrite', 1);
```

### Behavior

- **No connection string configured** (`EventFilterConnectionString` is empty): ALL events are captured (same as before).
- **Connection string configured, table is empty**: ALL events are captured (fail-open).
- **Connection string configured, table has rows**: Only events with `IsActive = 1` whose `EventName` matches the event type are captured.
- **Database unreachable**: Falls back to capturing ALL events (fail-open).
- The filter list is **automatically refreshed** from the DB at the interval set by `EventFilterRefreshIntervalSeconds` (default: 60 seconds). Set to `0` to only load once at startup.

### Valid EventName Values

| EventName | Description |
|-----------|-------------|
| `BeforeObjectWrite` | Object update, pre-DB |
| `AfterObjectWrite` | Object update, post-DB |
| `BeforeObjectDelete` | Object deletion, pre-DB |
| `AfterObjectDelete` | Object deletion, post-DB |
| `BeforeAttributeChange` | Attribute change, pre-DB |
| `AfterAttributeChange` | Attribute change, post-DB |
| `BeforeObjectCreate` | Object creation, pre-DB |
| `AfterObjectCreate` | Object creation, post-DB |

## How It Works

1. **COM Connection**: The app connects to the running COMOS instance via `Marshal.GetActiveObject()` (Running Object Table) or `Activator.CreateInstance()` using the COMOS ProgID
2. **Event Registration**: It registers a `ComosEventSink` (implementing `IComosDEventSink`) with COMOS's event manager using multiple fallback strategies:
   - `EventManager.AdviseEventSink()` 
   - `Workset.AdviseEventSink()`
   - Standard COM `IConnectionPoint.Advise()`
3. **STA Message Pump**: The main thread runs an STA message pump so COM callbacks are dispatched correctly
4. **JSON Logging**: Every event is serialized to a timestamped JSON file with full object/attribute details

## Adapting the Event Sink for Your COMOS Version

The exact COM interface names and event method signatures may vary between COMOS versions. To discover the correct interfaces:

1. Use **OLE/COM Object Viewer** (`oleview.exe`) to inspect the COMOS type library
2. Look for interfaces containing "Event", "Sink", "Notify", or "Callback" in their names
3. Check the COMOS SDK documentation (typically installed at `C:\Program Files (x86)\Siemens\COMOS\SDK\`)
4. Use **Visual Studio Object Browser** after adding the COM reference to explore available interfaces

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "Could not connect to COMOS" | Ensure COMOS is running with a project open |
| "No active workset" | Open a project and activate a workset in COMOS |
| Events not captured | Verify the COM interface GUIDs match your COMOS version |
| `COMException` | Run as the same user that launched COMOS; ensure x86 platform |
| "ProgID not found" | Check COMOS COM registration: `reg query HKCR\COMOS.Application` |

## License

This code is provided as-is for use with Siemens COMOS 10.5.2. Siemens and COMOS are trademarks of Siemens AG.
