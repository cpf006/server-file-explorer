using System;
using System.IO;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TestProject.Tests;

public class FileControllerTests : IAsyncLifetime
{
    private readonly string _rootDir;
    private readonly WebApplicationFactory<Program> _factory;

    public FileControllerTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_rootDir);
        Directory.CreateDirectory(Path.Combine(_rootDir, "sub"));
        File.WriteAllText(Path.Combine(_rootDir, "root.txt"), "root");
        File.WriteAllText(Path.Combine(_rootDir, "sub", "a.txt"), "hello");

        Environment.SetEnvironmentVariable("FileExplorer__RootPath", _rootDir);
        _factory = new WebApplicationFactory<Program>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable("FileExplorer__RootPath", null);
        Directory.Delete(_rootDir, true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ListRoot_ReturnsDirectoriesAndFiles()
    {
        var client = _factory.CreateClient();
        var result = await client.GetFromJsonAsync<ListResponse>("/api/files");

        Assert.NotNull(result);
        Assert.Contains("sub", result!.Directories);
        Assert.Contains(result.Files, f => f.Name == "root.txt");
        Assert.Equal(1, result.Stats.DirectoryCount);
        Assert.Equal(1, result.Stats.FileCount);
    }

    [Fact]
    public async Task Search_ReturnsMatches()
    {
        var client = _factory.CreateClient();
        var result = await client.GetFromJsonAsync<SearchResponse>("/api/files/search?query=a.txt");

        Assert.NotNull(result);
        Assert.Contains(result!.Files, f => f.Path.EndsWith("sub/a.txt"));
    }

    private class ListResponse
    {
        public string[] Directories { get; set; } = Array.Empty<string>();
        public FileItem[] Files { get; set; } = Array.Empty<FileItem>();
        public Stats Stats { get; set; } = new();
    }

    private class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    private class Stats
    {
        public int DirectoryCount { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
    }

    private class SearchResponse
    {
        public DirectoryResult[] Directories { get; set; } = Array.Empty<DirectoryResult>();
        public FileResult[] Files { get; set; } = Array.Empty<FileResult>();
    }

    private class DirectoryResult
    {
        public string Path { get; set; } = string.Empty;
    }

    private class FileResult
    {
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}

