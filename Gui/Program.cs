using Avalonia;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using PtzJoystickControl.Gui.ViewModels;
using PtzJoystickControl.Gui.TrayIcon;
using PtzJoystickControl.Application.Db;
using PtzJoystickControl.Application.Services;
using PtzJoystickControl.Core.Db;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.SdlGamepads.Services;
using Splat;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Octokit;
using System.Reflection;

namespace PtzJoystickControl.Gui;

internal class Program
{
    private static FileStream? logFile;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        string logDir;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".PTZJoystickControl/");
        else
            logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PTZJoystickControl/");

        Directory.CreateDirectory(logDir);
        string logPath = Path.Combine(logDir, "log.txt");
        logFile = File.OpenWrite(logPath);
        Trace.Listeners.Add(new TextWriterTraceListener(logFile));
        Debug.AutoFlush = true;

        var appBuilder = BuildAvaloniaApp();

        // Mutex to ensure only one instance will 
        // var mutex = new Mutex(false, "PTZJoystickControlMutex/BFD0A32E-F433-49E7-AB74-B49FC95012D0");
        try
        {
            // if (!mutex.WaitOne(0, false))
            // {
            //     appBuilder.StartWithClassicDesktopLifetime(new string[] { "-r" }, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
            //     return;
            // }

            RegisterServices();

            appBuilder.StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        }
        finally
        {
            // mutex?.Close();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();

    private static void RegisterServices()
    {
        var services = Locator.CurrentMutable;
        var resolver = Locator.Current;
        var avaloniaLocator = AvaloniaLocator.Current;

        services.Register<IGitHubClient>(() => new GitHubClient(new ProductHeaderValue("PTZJoystickControl-UpdateChecker")));
        services.Register<IUpdateService>(() => new UpdateService(
            resolver.GetServiceOrThrow<IGitHubClient>(),
            "AronHetLam",
            "PTZJoystickControl",
            Assembly.GetExecutingAssembly().GetName().Version!));

        services.RegisterConstant<ICameraSettingsStore>(new CameraSettingsStore());
        services.RegisterConstant<IGamepadSettingsStore>(new GamepadSettingsStore());

        services.RegisterConstant<ICommandsService>(new CommandsService());
        services.RegisterConstant<ICamerasService>(new CamerasService(
            resolver.GetServiceOrThrow<ICameraSettingsStore>()));
        services.RegisterConstant<IGamepadsService>(new SdlGamepadsService(
            resolver.GetServiceOrThrow<IGamepadSettingsStore>(),
            resolver.GetServiceOrThrow<ICamerasService>(),
            resolver.GetServiceOrThrow<ICommandsService>()));

        services.RegisterLazySingleton(() => new GamepadsViewModel(
            resolver.GetServiceOrThrow<IGamepadsService>()));
        services.Register(() => new CamerasViewModel(
            resolver.GetServiceOrThrow<ICamerasService>(),
            resolver.GetServiceOrThrow<GamepadsViewModel>()));
        services.RegisterLazySingleton(() => new TrayIconHandler(
            avaloniaLocator.GetServiceOrThrow<IAssetLoader>()));
    }
}

internal static class ResolverExtension
{
    internal static T GetServiceOrThrow<T>(this IReadonlyDependencyResolver resolver)
    {
        return resolver.GetService<T>()
            ?? throw new Exception("Resolved dependency cannot be null");
    }

    internal static T GetServiceOrThrow<T>(this IAvaloniaDependencyResolver resolver)
    {
        return resolver.GetService<T>()
            ?? throw new Exception("Resolved dependency cannot be null");
    }
}
