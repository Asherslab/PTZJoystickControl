using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PtzJoystickControl.Application.Commands;

// Presses Wirecast's "Go" button (preview -> live) via Wirecast's Windows COM automation interface.
// Must derive from IStaticCommand: Input only dispatches to IStaticCommand / IDynamicCommand / InputEnablerCommand,
// and IStaticCommand already handles press-edge detection.
public class WirecastLivePreviewCommand : IStaticCommand
{
    // Wirecast has 5 master layers, indexed 1..5.
    private const int LayerCount = 5;
    private const string ProgId = "Wirecast.Application";

    private int _running;

    public WirecastLivePreviewCommand(IGamepad gamepad) : base(gamepad)
    {
    }

    public override string CommandName => "Wirecast Live/Preview Toggle";

    public override string AxisParameterName => "Action";

    public override string ButtonParameterName => "Action";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[]
    {
        new CommandValueOption("Go (Preview to Live)", 0),
    };

    // There is only one action, so don't depend on the saved option (it may be stale or null).
    public override void Execute(CommandValueOption value) => Execute(0);

    public override void Execute(int value)
    {
        Trace.WriteLine("[Wirecast] Button pressed");

        if (!OperatingSystem.IsWindows())
        {
            Trace.WriteLine("[Wirecast] COM automation is only available on Windows");
            return;
        }

        // Ignore presses while a previous Go is still being sent.
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return;

        // COM automation is most reliable from an STA thread.
        var thread = new Thread(() =>
        {
            try { if (OperatingSystem.IsWindows()) Go(); }
            catch (Exception ex) { Trace.WriteLine($"[Wirecast] Go failed: {Unwrap(ex)}"); }
            finally { Interlocked.Exchange(ref _running, 0); }
        })
        { IsBackground = true, Name = "Wirecast Go" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    [SupportedOSPlatform("windows")]
    private static void Go()
    {
        object wirecast = GetWirecast();
        object? document = null;
        try
        {
            document = Invoke(wirecast, "DocumentByIndex", 1)
                ?? throw new InvalidOperationException("No Wirecast document is open");
            Trace.WriteLine("[Wirecast] Got document 1");

            // The UI's Go button takes every layer's preview shot live.
            for (int i = 1; i <= LayerCount; i++)
            {
                object? layer = null;
                try
                {
                    layer = Invoke(document, "LayerByIndex", i);
                    if (layer == null)
                    {
                        Trace.WriteLine($"[Wirecast] Layer {i} not found");
                        continue;
                    }
                    Invoke(layer, "Go");
                    Trace.WriteLine($"[Wirecast] Go sent to layer {i}");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Wirecast] Go on layer {i} failed: {Unwrap(ex)}");
                }
                finally
                {
                    if (layer != null) Marshal.FinalReleaseComObject(layer);
                }
            }
        }
        finally
        {
            if (document != null) Marshal.FinalReleaseComObject(document);
            Marshal.FinalReleaseComObject(wirecast);
        }
    }

    [SupportedOSPlatform("windows")]
    private static object GetWirecast()
    {
        try
        {
            object wirecast = Marshal2.GetActiveObject(ProgId);
            Trace.WriteLine("[Wirecast] Attached to running Wirecast");
            return wirecast;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Wirecast] GetActiveObject failed ({ex.Message}), trying CreateObject");
        }

        // Equivalent to VBScript CreateObject("Wirecast.Application"), which Wirecast's own
        // examples use; Wirecast is single-instance so this returns the running app.
        Type type = Type.GetTypeFromProgID(ProgId)
            ?? throw new InvalidOperationException($"{ProgId} is not registered. Is Wirecast installed?");
        object instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create {ProgId}");
        Trace.WriteLine("[Wirecast] Connected via CreateObject");
        return instance;
    }

    // InvokeMember wraps COM errors in TargetInvocationException, hiding the useful message.
    private static Exception Unwrap(Exception ex) => ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

    private static object? Invoke(object target, string method, params object[] args) =>
        target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args);
}
