using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using System.Diagnostics;
using System.Reflection;

namespace PtzJoystickControl.Application.Commands;

public class WirecastLivePreviewCommand : ICommand
{
    private readonly IGamepad _gamepad;
    private int _previousValue = 0;

    public WirecastLivePreviewCommand(IGamepad gamepad)
    {
        _gamepad = gamepad;
    }

    private const string CommandNameString = "Wirecast Live/Preview Toggle";
    public        string CommandName => CommandNameString;

    public string AxisParameterName   => "Action";
    public string ButtonParameterName => "Action";

    public IEnumerable<CommandValueOption> Options
    {
        get { yield return new CommandValueOption("Toggle Live/Preview", 0); }
    }

    public void Execute(int value)
    {
        // Only trigger on button press (transition from 0 to non-zero)
        if (_previousValue == 0 && value != 0)
        {
            Debug.WriteLine($"[WirecastLivePreview] Button pressed, triggering COM automation");
            // Use official Wirecast COM automation interface
            _ = Task.Run(TryWirecastComAutomation);
        }

        _previousValue = value;
    }

    public void Execute(CommandValueOption value)
    {
        if (value != null)
            Execute(value.Value);
    }

    private bool TryWirecastComAutomation()
    {
        try
        {
            Debug.WriteLine("[WirecastLivePreview] Starting COM automation");

            // Get the running Wirecast application via COM
            object? wirecast = GetWirecastApplication();
            if (wirecast == null)
            {
                Debug.WriteLine("[WirecastLivePreview] Failed to get Wirecast application via COM");
                return false;
            }
            Debug.WriteLine("[WirecastLivePreview] Successfully got Wirecast application");

            // Get the active document (current project)
            object? document = InvokeMethod(wirecast, "DocumentByIndex", 1);
            if (document == null)
            {
                Debug.WriteLine("[WirecastLivePreview] Failed to get document by index 1");
                return false;
            }
            Debug.WriteLine("[WirecastLivePreview] Successfully got document");

            // Get the "normal" layer (layer 3) where shots are typically located
            object? layer = InvokeMethod(document, "LayerByName", "normal");
            if (layer == null)
            {
                Debug.WriteLine("[WirecastLivePreview] Failed to get layer by name 'normal'");
                return false;
            }
            Debug.WriteLine("[WirecastLivePreview] Successfully got 'normal' layer");

            // Get the preview shot ID
            object? previewShotId = InvokeMethod(layer, "PreviewShotID");
            if (previewShotId == null || !(previewShotId is int shotId) || shotId == 0)
            {
                Debug.WriteLine($"[WirecastLivePreview] Failed to get preview shot ID (got: {previewShotId})");
                return false;
            }
            Debug.WriteLine($"[WirecastLivePreview] Got preview shot ID: {shotId}");

            // Set the preview shot as the active shot (equivalent to clicking on it)
            SetProperty(layer, "ActiveShotID", shotId);
            Debug.WriteLine($"[WirecastLivePreview] Set ActiveShotID to {shotId}");

            // Check if AutoLive is off - if so, we need to call Go to make it live
            object? autoLiveValue = GetProperty(document, "AutoLive");
            if (autoLiveValue is int autoLive && autoLive == 0)
            {
                Debug.WriteLine("[WirecastLivePreview] AutoLive is off, calling Go()");
                InvokeMethod(layer, "Go");
                Debug.WriteLine("[WirecastLivePreview] Go() called successfully");
            }
            else
            {
                Debug.WriteLine($"[WirecastLivePreview] AutoLive is on (value: {autoLiveValue}), Go() not needed");
            }

            Debug.WriteLine("[WirecastLivePreview] COM automation completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WirecastLivePreview] Exception in COM automation: {ex.Message}");
            return false;
        }
    }

    private static object? GetWirecastApplication()
    {
        try
        {
            // Try to get the running Wirecast instance (Gameshow.Application is the newer ProgID)
            return Marshal2.GetActiveObject("Gameshow.Application");
        }
        catch
        {
            // Fallback to the older ProgID
            return Marshal2.GetActiveObject("Wirecast.Application");
        }
    }

    private static object? InvokeMethod(object obj, string methodName, params object[] parameters)
    {
        try
        {
            return obj.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                obj,
                parameters);
        }
        catch
        {
            return null;
        }
    }

    private static void SetProperty(object obj, string propertyName, object value)
    {
        try
        {
            obj.GetType().InvokeMember(
                propertyName,
                BindingFlags.SetProperty,
                null,
                obj,
                new object[] { value });
        }
        catch
        {
            // Ignore property set errors
        }
    }

    private static object? GetProperty(object obj, string propertyName)
    {
        try
        {
            return obj.GetType().InvokeMember(
                propertyName,
                BindingFlags.GetProperty,
                null,
                obj,
                null);
        }
        catch
        {
            return null;
        }
    }
}