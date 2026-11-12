using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Monitor = Silk.NET.Windowing.Monitor;

namespace FishPaper;

public enum WindowMode
{
    Background,
    Main
}

public class WindowsNotCreatedYetException : Exception
{
    public WindowsNotCreatedYetException() { }
    public WindowsNotCreatedYetException(string message) : base(message) { }
}

public class Windows
{
    private static Windows? _instance;

    private readonly List<UWindow> _uWindows = new();
    private int _backgroundCount = 0;

    private Windows() { }

    public static Windows GetInstance() => _instance ??= new Windows();

    public void CreateWindow(WindowMode mode)
    {
        UWindow uWindow;
        
        if (mode == WindowMode.Main)
            uWindow = new UWindow(true);
        else
            uWindow = new UWindow(false);
        
        WindowOptions options = WindowOptions.Default;

        switch (mode)
        {
            case WindowMode.Background:
                ConfigureBackground(ref options, ref uWindow, _backgroundCount);
                _backgroundCount++;
                break;

            case WindowMode.Main:
                ConfigureMain(ref options);
                break;
        }

        uWindow.SetOptions(options);
        _uWindows.Add(uWindow);
    }

    private static void ConfigureBackground(ref WindowOptions options, ref UWindow uWindow, int index)
    {
        var monitors = Monitor.GetMonitors(null).ToList();
        if (index >= monitors.Count)
            throw new InvalidOperationException(
                $"Requested monitor {index}, but Silk.NET detected {monitors.Count}.");

        var wallpapers = Wallpapers.GetWallpapers();
        if (index >= wallpapers.Count)
            throw new InvalidOperationException(
                $"Requested wallpaper {index}, but only {wallpapers.Count} are available.");

        var monitor = monitors[index];

        options.Title = $"Wallpaper Window {index + 1}";
        options.WindowBorder = WindowBorder.Hidden;
        options.Position = monitor.Bounds.Origin + new Vector2D<int>(0, -50);
        options.Size = monitor.Bounds.Size + new Vector2D<int>(0, 50);

        uWindow.SetImage(wallpapers[index]);
        Console.WriteLine($"Wallpaper on monitor {index}: {wallpapers[index]}");
    }

    private static void ConfigureMain(ref WindowOptions options)
    {
        var monitors = Monitor.GetMonitors(null).ToList();

        options.Title = "FishPaper";
        options.WindowBorder = WindowBorder.Fixed;
        options.Position = monitors.Count > 0 ? monitors[0].Bounds.Origin : new Vector2D<int>(0, 0);
        options.Size = new Vector2D<int>(860, 640);
        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,
            ContextProfile.Core,
            ContextFlags.ForwardCompatible,
            new APIVersion(3, 3));
    }

    public void RunWindows()
    {
        if (_uWindows.Count == 0)
            throw new WindowsNotCreatedYetException("No windows were created yet!");

        foreach (var uWindow in _uWindows)
            uWindow.Initialize();

        // Single loop driving all windows. Exits when any window is closed.
        while (!_uWindows.Any(w => w.IsClosing))
        {
            foreach (var uWindow in _uWindows)
                uWindow.Tick();

            Thread.Sleep(1);
        }

        foreach (var uWindow in _uWindows)
            uWindow.Dispose();

        _uWindows.Clear();
    }
}
