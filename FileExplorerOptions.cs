using System.IO;

namespace TestProject;

public class FileExplorerOptions
{
    public const string DefaultRootDirectoryName = "DefaultDirectory";

    // Root directory for all file work. The spec asks for a configurable home folder, so we default to a sandboxed "DefaultDirectory".
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, DefaultRootDirectoryName);
}

