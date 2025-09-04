using System;
using System.IO;
using Microsoft.Extensions.Options;

namespace TestProject.Services;

public class PathResolver
{
    private readonly string _root;

    public PathResolver(IOptions<FileExplorerOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath ?? Directory.GetCurrentDirectory());
    }

    public string Resolve(string? relative)
    {
        relative ??= string.Empty;
        var combined = Path.GetFullPath(Path.Combine(_root, relative));
        if (!combined.StartsWith(_root))
            throw new InvalidOperationException("Invalid path");
        return combined;
    }
}
