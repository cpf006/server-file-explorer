using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TestProject.Services;
using TestProject.Models;
using Xunit;

namespace TestProject.Tests;

public class FileServiceTests : IAsyncLifetime
{
    private readonly string _rootDir;
    private readonly FileService _service;

    public FileServiceTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_rootDir);
        Directory.CreateDirectory(Path.Combine(_rootDir, "sub"));
        File.WriteAllText(Path.Combine(_rootDir, "root.txt"), "root");
        File.WriteAllText(Path.Combine(_rootDir, "sub", "a.txt"), "hello");

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        File.Copy(Path.Combine(dataDir, "point.geojson"), Path.Combine(_rootDir, "point.geojson"));
        File.Copy(Path.Combine(dataDir, "point.kml"), Path.Combine(_rootDir, "point.kml"));

        var options = Options.Create(new FileExplorerOptions { RootPath = _rootDir });
        var resolver = new PathResolver(options);
        _service = new FileService(resolver);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        Directory.Delete(_rootDir, true);
        return Task.CompletedTask;
    }

    [Fact]
    public void ListRoot_ReturnsDirectoriesAndFiles()
    {
        var result = _service.List(null);
        Assert.Contains("sub", result.Directories);
        Assert.Contains(result.Files, f => f.Name == "root.txt");
        Assert.Contains(result.Files, f => f.Name == "point.geojson");
        Assert.Contains(result.Files, f => f.Name == "point.kml");
        Assert.Equal(1, result.Stats.DirectoryCount);
        Assert.Equal(3, result.Stats.FileCount);
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        _service.Delete("root.txt");
        Assert.False(File.Exists(Path.Combine(_rootDir, "root.txt")));
    }

    [Fact]
    public void Move_RenamesFile()
    {
        _service.Move("sub/a.txt", "sub/b.txt");
        Assert.True(File.Exists(Path.Combine(_rootDir, "sub", "b.txt")));
        Assert.False(File.Exists(Path.Combine(_rootDir, "sub", "a.txt")));
    }

    [Fact]
    public void Copy_DuplicatesDirectory()
    {
        _service.Copy("sub", "sub_copy");
        Assert.True(Directory.Exists(Path.Combine(_rootDir, "sub_copy")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "sub_copy", "a.txt")));
    }

    [Fact]
    public async Task Zip_ReturnsArchive()
    {
        await using var stream = await _service.Zip(new List<string> { "root.txt", "sub" });
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("root.txt"));
        Assert.NotNull(archive.GetEntry("sub/a.txt"));
    }

    [Fact]
    public void Search_ReturnsMatches()
    {
        var result = _service.Search("a.txt");
        Assert.Contains(result.Files, f => f.Path.EndsWith("sub/a.txt"));
    }
}
