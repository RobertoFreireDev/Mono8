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

    public static void Write(string fileName, string extension, string content, string path = "")
    {
        var fullPath = BuildPath(fileName, extension, path);
        File.WriteAllText(fullPath, content);
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

    /// <summary>
    /// Mirrors the authored data files into the project's <c>publishdata</c> folder so a save from a
    /// dev run keeps the backup next to the sources. Silently does nothing when the project file
    /// cannot be found above the working directory, which is the case for a published build.
    /// </summary>
    public static void MirrorDataFiles(string sourcePath)
    {
        try
        {
            var projectPath = FindProjectFolder();
            if (projectPath == null) return;

            var targetPath = Path.Combine(projectPath, PublishFolder);
            Directory.CreateDirectory(targetPath);

            foreach (var file in Directory.GetFiles(sourcePath, $"{Constants.File.Name}.*"))
            {
                // data.save is runtime persistence (dset), not authored data.
                if (Path.GetExtension(file) == $".{Constants.File.Extensions.Save}") continue;
                File.Copy(file, Path.Combine(targetPath, Path.GetFileName(file)), true);
            }
        }
        catch
        {
            // A locked or read-only backup must never break the save itself.
        }
    }

    private const string PublishFolder = "publishdata";

    private const string ProjectFile = "mono8.csproj";

    private static string FindProjectFolder()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ProjectFile))) return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }
}
