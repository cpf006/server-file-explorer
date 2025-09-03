using System.IO;

namespace TestProject;

public class FileExplorerOptions
{
    // Root directory for all file operations ("A server side home directory should be configurable via variable."). Uses a safe default folder.
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "DefaultDirectory");
}

