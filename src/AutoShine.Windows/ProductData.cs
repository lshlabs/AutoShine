namespace AutoShine.Windows;

public static class ProductData
{
    public static string Root() => ResolveRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    public static string ResolveRoot(string local)
    {
        var current = Path.Combine(local, "AutoShine");
        var previous = Path.Combine(local, "SunshineCompanion");
        if (!Directory.Exists(current) && Directory.Exists(previous))
        {
            try { Directory.Move(previous, current); }
            catch (IOException) when (Directory.Exists(current) && !Directory.Exists(previous)) { /* Another process finished the same migration. */ }
        }
        return current;
    }
}
