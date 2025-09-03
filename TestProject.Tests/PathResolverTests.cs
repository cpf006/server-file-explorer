using System;
using System.IO;
using Microsoft.Extensions.Options;
using TestProject.Services;
using Xunit;

namespace TestProject.Tests;

public class PathResolverTests
{
    [Fact]
    public void ResolvePath_ValidPath_ReturnsFullPath()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        try
        {
            var options = Options.Create(new FileExplorerOptions { RootPath = root });
            var resolver = new PathResolver(options);
            var full = resolver.ResolvePath("sub/file.txt");
            Assert.Equal(Path.GetFullPath(Path.Combine(root, "sub/file.txt")), full);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ResolvePath_InvalidPath_Throws()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        try
        {
            var options = Options.Create(new FileExplorerOptions { RootPath = root });
            var resolver = new PathResolver(options);
            Assert.Throws<InvalidOperationException>(() => resolver.ResolvePath("../outside.txt"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
