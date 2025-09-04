using System;
using System.IO;
using Microsoft.Extensions.Options;
using TestProject;
using TestProject.Services;
using Xunit;

namespace TestProject.Tests;

public class PathResolverTests : IDisposable
{
    private readonly string _root;
    private readonly PathResolver _resolver;

    public PathResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
        _resolver = new PathResolver(Options.Create(new FileExplorerOptions { RootPath = _root }));
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("sub/../../outside.txt")]
    public void Resolve_DetectsTraversal(string input)
    {
        Assert.Throws<InvalidOperationException>(() => _resolver.Resolve(input));
    }

    [Fact]
    public void Resolve_DetectsAbsoluteEscape()
    {
        var outside = Path.GetFullPath(Path.Combine(_root, "..", "outside.txt"));
        Assert.Throws<InvalidOperationException>(() => _resolver.Resolve(outside));
    }
}
