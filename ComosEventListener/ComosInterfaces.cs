// =============================================================================
// ComosInterfaces.cs
// 
// COM Interface definitions for Siemens COMOS 10.5.2
// These mirror the COMOS COM type library interfaces needed for event handling.
// When you have COMOS installed, you can replace these with proper COM references
// generated via tlbimp or Visual Studio's "Add COM Reference".
// =============================================================================

using System;
using System.Runtime.InteropServices;

namespace ComosEventListener.ComosInterop
{
    // =========================================================================
    // IComosDDevice - Represents a COMOS object (device/element)
    // =========================================================================
    [ComImport]
    [Guid("00000000-0000-0000-0000-000000000001")] // Replace with actual COMOS GUID
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IComosDDevice
    {
        [DispId(1)]
        string SystemFullName { get; }

        [DispId(2)]
        string Name { get; }

        [DispId(3)]
        string Label { get; }

        [DispId(4)]
        string Description { get; }

        [DispId(5)]
        string SystemType { get; }

        [DispId(6)]
        string UID { get; }

        [DispId(7)]
        object Specifications { get; }

        [DispId(8)]
        object Owner { get; }
    }

    // =========================================================================
    // IComosDSpecification - Represents an attribute/specification on an object
    // =========================================================================
    [ComImport]
    [Guid("00000000-0000-0000-0000-000000000002")] // Replace with actual COMOS GUID
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IComosDSpecification
    {
        [DispId(1)]
        string Name { get; }

        [DispId(2)]
        string Label { get; }

        [DispId(3)]
        string Description { get; }

        [DispId(4)]
        object Value { get; }

        [DispId(5)]
        string DisplayValue { get; }

        [DispId(6)]
        string Unit { get; }

        [DispId(7)]
        string SystemFullName { get; }

        [DispId(8)]
        IComosDDevice Owner { get; }
    }

    // =========================================================================
    // IComosDWorkset - Represents the COMOS working environment / workset
    // =========================================================================
    [ComImport]
    [Guid("00000000-0000-0000-0000-000000000003")] // Replace with actual COMOS GUID
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IComosDWorkset
    {
        [DispId(1)]
        string Name { get; }

        [DispId(2)]
        IComosDProject Project { get; }

        [DispId(3)]
        object CurrentUser { get; }
    }

    // =========================================================================
    // IComosDProject - Represents a COMOS project
    // =========================================================================
    [ComImport]
    [Guid("00000000-0000-0000-0000-000000000004")] // Replace with actual COMOS GUID
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IComosDProject
    {
        [DispId(1)]
        string Name { get; }

        [DispId(2)]
        string Description { get; }
    }

    // =========================================================================
    // IComosDEventManager - COMOS Event Manager for subscribing to events
    //
    // The COMOS kernel publishes events through its COM event infrastructure.
    // Key event categories:
    //   - BeforeWrite / AfterWrite  (pre/post DB commit)
    //   - BeforeDelete / AfterDelete
    //   - OnObjectChanged / OnAttributeChanged
    //   - OnObjectCreated / OnObjectDeleted
    // =========================================================================
    [ComImport]
    [Guid("00000000-0000-0000-0000-000000000005")] // Replace with actual COMOS GUID
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IComosDEventManager
    {
        [DispId(1)]
        void AdviseEventSink(object eventSink);

        [DispId(2)]
        void UnadviseEventSink(object eventSink);
    }

    // =========================================================================
    // IComosDEventSink - Interface that your event handler must implement
    //
    // COMOS calls these methods on registered sinks BEFORE committing to the DB.
    // This is the primary mechanism for intercepting pre-update events.
    // =========================================================================
    [ComImport]
    [Guid("00000000-0000-0000-0000-000000000006")] // Replace with actual COMOS GUID
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IComosDEventSink
    {
        /// <summary>
        /// Fired before an object is written (saved) to the database.
        /// </summary>
        [DispId(1)]
        void OnBeforeObjectWrite(object comosObject);

        /// <summary>
        /// Fired after an object has been written to the database.
        /// </summary>
        [DispId(2)]
        void OnAfterObjectWrite(object comosObject);

        /// <summary>
        /// Fired before an object is deleted from the database.
        /// </summary>
        [DispId(3)]
        void OnBeforeObjectDelete(object comosObject);

        /// <summary>
        /// Fired after an object has been deleted from the database.
        /// </summary>
        [DispId(4)]
        void OnAfterObjectDelete(object comosObject);

        /// <summary>
        /// Fired when an attribute/specification value changes (before DB commit).
        /// </summary>
        [DispId(5)]
        void OnBeforeAttributeChange(object specification, object oldValue, object newValue);

        /// <summary>
        /// Fired after an attribute/specification value change is committed.
        /// </summary>
        [DispId(6)]
        void OnAfterAttributeChange(object specification);

        /// <summary>
        /// Fired when a new object is about to be created.
        /// </summary>
        [DispId(7)]
        void OnBeforeObjectCreate(object comosObject);

        /// <summary>
        /// Fired after a new object has been created.
        /// </summary>
        [DispId(8)]
        void OnAfterObjectCreate(object comosObject);
    }
}
