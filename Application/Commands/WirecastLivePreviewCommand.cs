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

    public override void Execute(int value)
    {
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
            catch (Exception ex) { Trace.WriteLine($"[Wirecast] Go failed: {ex}"); }
            finally { Interlocked.Exchange(ref _running, 0); }
        })
        { IsBackground = true, Name = "Wirecast Go" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    [SupportedOSPlatform("windows")]
    private static void Go()
    {
        object wirecast = Marshal2.GetActiveObject("Wirecast.Application");
        object? document = null;
        try
        {
            document = Invoke(wirecast, "DocumentByIndex", 1)
                ?? throw new InvalidOperationException("No Wirecast document is open");

            // The UI's Go button takes every layer's preview shot live.
            for (int i = 1; i <= LayerCount; i++)
            {
                object? layer = Invoke(document, "LayerByIndex", i);
                if (layer == null) continue;
                try
                {
                    Invoke(layer, "Go");
                }
                finally
                {
                    Marshal.FinalReleaseComObject(layer);
                }
            }

            Trace.WriteLine("[Wirecast] Go sent");
        }
        finally
        {
            if (document != null) Marshal.FinalReleaseComObject(document);
            Marshal.FinalReleaseComObject(wirecast);
        }
    }

    private static object? Invoke(object target, string method, params object[] args) =>
        target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args);
}
