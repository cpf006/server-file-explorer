using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using TestProject.Services;
using TestProject.Models;

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
        _ = _factory.Services.GetRequiredService<PathResolver>();
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
    public async Task ListRootReturnsDirectoriesAndFiles()
    {
        var client = _factory.CreateClient();
        var result = await client.GetFromJsonAsync<DirectoryListing>("/api/files");

        Assert.NotNull(result);
        Assert.Contains("sub", result!.Directories);
        Assert.Contains(result.Files, f => f.Name == "root.txt");
        Assert.Contains(result.Files, f => f.Name == "point.geojson");
        Assert.Contains(result.Files, f => f.Name == "point.kml");
        Assert.Equal(1, result.Stats.DirectoryCount);
        Assert.Equal(3, result.Stats.FileCount);
    }

    [Fact]
    public async Task SearchReturnsMatches()
    {
        var client = _factory.CreateClient();

        var fileResult = await client.GetFromJsonAsync<SearchResult>("/api/files/search?query=a.txt");
        Assert.NotNull(fileResult);
        Assert.Contains(fileResult!.Files, f => f.Path.EndsWith("sub/a.txt"));

        var dirResult = await client.GetFromJsonAsync<SearchResult>("/api/files/search?query=sub");
        Assert.NotNull(dirResult);
        Assert.Contains(dirResult!.Directories, d => d.Path == "sub");
    }

    [Fact]
    public async Task DeleteRemovesFile()
    {
        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/files?path=root.txt");
        response.EnsureSuccessStatusCode();
        Assert.False(System.IO.File.Exists(Path.Combine(_rootDir, "root.txt")));
    }

    [Fact]
    public async Task MkdirCreatesDirectory()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/files/mkdir?path=newdir", null);
        response.EnsureSuccessStatusCode();
        Assert.True(Directory.Exists(Path.Combine(_rootDir, "newdir")));
    }

    [Fact]
    public async Task MoveRenamesFile()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/files/move", new { from = "sub/a.txt", to = "sub/b.txt" });
        response.EnsureSuccessStatusCode();
        Assert.True(System.IO.File.Exists(Path.Combine(_rootDir, "sub", "b.txt")));
        Assert.False(System.IO.File.Exists(Path.Combine(_rootDir, "sub", "a.txt")));
    }

    [Fact]
    public async Task CopyDuplicatesDirectory()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/files/copy", new { from = "sub", to = "sub_copy" });
        response.EnsureSuccessStatusCode();
        Assert.True(Directory.Exists(Path.Combine(_rootDir, "sub_copy")));
        Assert.True(System.IO.File.Exists(Path.Combine(_rootDir, "sub_copy", "a.txt")));
    }

    [Fact]
    public async Task UploadAllowsLargeFiles()
    {
        var client = _factory.CreateClient();
        var content = new MultipartFormDataContent();
        var bytes = new byte[40 * 1024 * 1024];
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "big.bin");
        var response = await client.PostAsync("/api/files/upload", content);
        response.EnsureSuccessStatusCode();
        Assert.True(System.IO.File.Exists(Path.Combine(_rootDir, "big.bin")));
    }

    [Fact]
    public async Task ZipReturnsArchive()
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
    public async Task PreviewReturnsFileContent()
    {
        var client = _factory.CreateClient();
        var text = await client.GetStringAsync("/api/preview?path=root.txt");
        Assert.Equal("root", text.Trim());
    }

    [Fact]
    public async Task PreviewConvertsKmlToGeoJson()
    {
        var client = _factory.CreateClient();
        var json = await client.GetStringAsync("/api/preview?path=point.kml");
        using var doc = JsonDocument.Parse(json);
        var coords = doc.RootElement.GetProperty("features")[0]
            .GetProperty("geometry").GetProperty("coordinates");
        Assert.Equal(1.0, coords[0].GetDouble());
        Assert.Equal(2.0, coords[1].GetDouble());
    }

}

