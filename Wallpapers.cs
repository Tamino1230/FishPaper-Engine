namespace FishPaper;

public class Wallpapers
{
    private static string _directoryName = "wallpapers";
    private static string _fullPath = Path.Combine(AppContext.BaseDirectory, _directoryName);
    private static string[] _extensions = { ".png", ".jpg", ".jpeg" };

    private static void EnsureExists()
    {
        if (!Path.Exists(_fullPath))
        {
            Directory.CreateDirectory(_fullPath);
        }
    }

    private static string GetFileExtension(string filename)
    {
        return Path.GetExtension(filename);
    }

    public static List<string> GetWallpapers()
    {
        EnsureExists();
        List<string> wallpapers = new List<string>();
        
        string[] files = Directory.GetFiles(_fullPath);
        foreach (string file in files)
        {
            string extension = GetFileExtension(file);
            if (!_extensions.Contains(extension))
                continue;
            
            wallpapers.Add(file);
        }

        return wallpapers;
    }
}