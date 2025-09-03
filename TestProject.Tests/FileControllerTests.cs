using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
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

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        File.Copy(Path.Combine(dataDir, "point.geojson"), Path.Combine(_rootDir, "point.geojson"));
        File.Copy(Path.Combine(dataDir, "point.kml"), Path.Combine(_rootDir, "point.kml"));

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
        Assert.Contains(result.Files, f => f.Name == "point.geojson");
        Assert.Contains(result.Files, f => f.Name == "point.kml");
        Assert.Equal(1, result.Stats.DirectoryCount);
        Assert.Equal(3, result.Stats.FileCount);
    }

    [Fact]
    public async Task Search_ReturnsMatches()
    {
        var client = _factory.CreateClient();
        var result = await client.GetFromJsonAsync<SearchResponse>("/api/files/search?query=a.txt");

        Assert.NotNull(result);
        Assert.Contains(result!.Files, f => f.Path.EndsWith("sub/a.txt"));
    }

    [Fact]
    public async Task Delete_RemovesFile()
    {
        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/files?path=root.txt");
        response.EnsureSuccessStatusCode();
        Assert.False(System.IO.File.Exists(Path.Combine(_rootDir, "root.txt")));
    }

    [Fact]
    public async Task Delete_RemovesEmptyParentDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "a", "b"));
        File.WriteAllText(Path.Combine(_rootDir, "a", "b", "c.txt"), "hi");
        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/files?path=" + Uri.EscapeDataString("a/b/c.txt"));
        response.EnsureSuccessStatusCode();
        Assert.False(Directory.Exists(Path.Combine(_rootDir, "a", "b")));
        Assert.False(Directory.Exists(Path.Combine(_rootDir, "a")));
    }

    [Fact]
    public async Task Mkdir_CreatesDirectory()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/files/mkdir?path=newdir", null);
        response.EnsureSuccessStatusCode();
        Assert.True(Directory.Exists(Path.Combine(_rootDir, "newdir")));
    }

    [Fact]
    public async Task Move_RenamesFile()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/files/move", new { from = "sub/a.txt", to = "sub/b.txt" });
        response.EnsureSuccessStatusCode();
        Assert.True(System.IO.File.Exists(Path.Combine(_rootDir, "sub", "b.txt")));
        Assert.False(System.IO.File.Exists(Path.Combine(_rootDir, "sub", "a.txt")));
    }

    [Fact]
    public async Task Copy_DuplicatesDirectory()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/files/copy", new { from = "sub", to = "sub_copy" });
        response.EnsureSuccessStatusCode();
        Assert.True(Directory.Exists(Path.Combine(_rootDir, "sub_copy")));
        Assert.True(System.IO.File.Exists(Path.Combine(_rootDir, "sub_copy", "a.txt")));
    }

    [Fact]
    public async Task Zip_ReturnsArchive()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/files/zip", new { paths = new[] { "root.txt", "sub" } });
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("root.txt"));
        Assert.NotNull(archive.GetEntry("sub/a.txt"));
    }

    [Fact]
    public async Task GeoPreview_ReturnsGeoJson_ForGeoJsonFile()
    {
        var client = _factory.CreateClient();
        var json = await client.GetStringAsync("/api/files/geo-preview?path=point.geojson");
        using var doc = JsonDocument.Parse(json);
        var coords = doc.RootElement.GetProperty("features")[0]
            .GetProperty("geometry").GetProperty("coordinates");
        Assert.Equal(1.0, coords[0].GetDouble());
        Assert.Equal(2.0, coords[1].GetDouble());
    }

    [Fact]
    public async Task GeoPreview_ConvertsKmlToGeoJson()
    {
        var client = _factory.CreateClient();
        var json = await client.GetStringAsync("/api/files/geo-preview?path=point.kml");
        using var doc = JsonDocument.Parse(json);
        var coords = doc.RootElement.GetProperty("features")[0]
            .GetProperty("geometry").GetProperty("coordinates");
        Assert.Equal(1.0, coords[0].GetDouble());
        Assert.Equal(2.0, coords[1].GetDouble());
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

