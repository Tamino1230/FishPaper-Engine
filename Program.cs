using System.Net;

namespace FishPaper;

class Program
{
    static void Main(string[] args)
    {
        FishPaperApp app = FishPaperApp.GetInstance();
        app.Run();
    }
}

public class FishPaperApp
{
    private static FishPaperApp? _fishPaperApp;
    
    private List<string> _wallpapers = Wallpapers.GetWallpapers();
    private Windows _windows = Windows.GetInstance();
    
    private FishPaperApp() { }

    public static FishPaperApp GetInstance()
    {
        _fishPaperApp ??= new FishPaperApp();
        return _fishPaperApp;
    }

    public void Run()
    {
        _windows.CreateWindow(WindowMode.Main);
        _windows.CreateWindow(WindowMode.Background);
        _windows.CreateWindow(WindowMode.Background);
        _windows.RunWindows();
    }
}
