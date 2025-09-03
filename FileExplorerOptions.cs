using System.IO;

namespace TestProject;

public class FileExplorerOptions
{
    /// <summary>
    /// Root directory for all file system operations.
    /// </summary>
    public string RootPath { get; set; } = Directory.GetCurrentDirectory();
}

