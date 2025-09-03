using System.IO;

namespace TestProject;

public class FileExplorerOptions
{
    /*
     * Root directory for browsing and uploads.
     * Defaults to a "files-default" folder so we don't expose the project files.
     */
    public string RootPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "files-default");
}

