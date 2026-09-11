namespace Omnom.Api.Services;

public static class EnvironmentFile
{
    public static void Load(string workingDirectory)
    {
        var directory = new DirectoryInfo(workingDirectory);
        var path = Path.Combine(directory.FullName, ".env");
        if (!File.Exists(path) && directory.Name == "backend" && directory.Parent != null)
            path = Path.Combine(directory.Parent.FullName, ".env");
        if (!File.Exists(path)) return;

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var separator = trimmed.IndexOf('=');
            if (separator <= 0) continue;
            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            if (Environment.GetEnvironmentVariable(key) == null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
