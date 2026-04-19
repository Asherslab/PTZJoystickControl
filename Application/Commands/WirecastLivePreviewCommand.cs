using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PtzJoystickControl.Application.Commands;

public class WirecastLivePreviewCommand : ICommand
{
    private readonly IGamepad _gamepad;

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
        // Use official Wirecast COM automation interface
        _ = Task.Run(TryWirecastComAutomation);
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
            // Get the running Wirecast application via COM
            object? wirecast = GetWirecastApplication();
            if (wirecast == null)
                return false;

            // Get the active document (current project)
            object? document = InvokeMethod(wirecast, "DocumentByIndex", 1);
            if (document == null)
                return false;

            // Get the "normal" layer (layer 3) where shots are typically located
            object? layer = InvokeMethod(document, "LayerByName", "normal");
            if (layer == null)
                return false;

            // Call the "Go" method on the layer to trigger live/preview switch
            InvokeMethod(layer, "Go");

            return true;
        }
        catch
        {
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
}