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
    public string CommandName => CommandNameString;

    public string AxisParameterName => "Action";
    public string ButtonParameterName => "Action";

    public IEnumerable<CommandValueOption> Options
    {
        get
        {
            yield return new CommandValueOption("Toggle Live/Preview", 0);
        }
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

            // Call the "Go" method on the document to trigger live/preview switch
            InvokeMethod(document, "Go");

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
            // Try to get the running Wirecast instance
            return Marshal2.GetActiveObject("Wirecast.Application");
        }
        catch
        {
            try
            {
                // If Wirecast isn't running, try to start it
                Type? wirecastType = Type.GetTypeFromProgID("Wirecast.Application");
                if (wirecastType != null)
                {
                    return Activator.CreateInstance(wirecastType);
                }
            }
            catch
            {
                // Wirecast not available
            }
            return null;
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
