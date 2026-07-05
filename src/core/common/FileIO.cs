namespace mono8.core.common;

public static class FileIO
{
    public static string Read(string fileName, string extension, string path = "")
    {
        try
        {
            var fullPath = BuildPath(fileName, extension, path);

            using (StreamReader reader = new StreamReader(fullPath))
            {
                return reader.ReadToEnd();
            }
        }
        catch
        {
            return string.Empty;
        }
        
    }

    public static string BuildPath(string fileName, string extension, string path)
    {
        var basePath = string.IsNullOrWhiteSpace(path)
            ? Directory.GetCurrentDirectory()
            : path;

        fileName += $".{extension}";
        return Path.Combine(basePath, fileName);
    }

    public static string[] SplitData(string data)
    {
        return data.Split('\n');
    }
}
