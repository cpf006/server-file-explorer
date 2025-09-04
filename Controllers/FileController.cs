using Microsoft.AspNetCore.Mvc;
using TestProject.Services;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using TestProject.Models;

namespace TestProject.Controllers;

// Web API for browsing and manipulating files within a configured root directory.
[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
    private readonly PathResolver _pathResolver;
    private readonly string _root;

    public FileController(PathResolver pathResolver)
    {
        _pathResolver = pathResolver;
        _root = _pathResolver.Resolve(null);
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string? path)
    {
        var full = _pathResolver.Resolve(path);
        if (!Directory.Exists(full))
            return NotFound();
        var directory = new DirectoryInfo(full);
        var directories = directory.GetDirectories().Select(d => d.Name).ToList();
        var files = directory.GetFiles().Select(f => new FileItem(f.Name, f.Length)).ToList();
        var stats = new Stats(directories.Count, files.Count, files.Sum(f => f.Size));
        var result = new DirectoryListing(path ?? string.Empty, directories, files, stats);
        return Ok(result);
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path)
    {
        var full = _pathResolver.Resolve(path);
        if (!System.IO.File.Exists(full))
            return NotFound();
        var fileName = Path.GetFileName(full);
        return PhysicalFile(full, "application/octet-stream", fileName);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Upload([FromQuery] string? path, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file supplied");

        var folder = _pathResolver.Resolve(path);
        if (!Directory.Exists(folder))
            return NotFound();

        var dest = Path.Combine(folder, Path.GetFileName(file.FileName));
        await using var stream = System.IO.File.Create(dest);
        await file.CopyToAsync(stream);
        return Ok();
    }

    [HttpPost("mkdir")]
    public IActionResult CreateDirectory([FromQuery] string path)
    {
        var full = _pathResolver.Resolve(path);
        if (System.IO.File.Exists(full))
            return Conflict();
        Directory.CreateDirectory(full);
        return Ok();
    }

    [HttpDelete]
    public IActionResult Delete([FromQuery] string path)
    {
        var full = _pathResolver.Resolve(path);
        if (System.IO.File.Exists(full))
            System.IO.File.Delete(full);
        else if (Directory.Exists(full))
            Directory.Delete(full, true);
        else
            return NotFound();
        return Ok();
    }

    [HttpPost("move")]
    public IActionResult Move([FromBody] PathRequest request)
    {
        try
        {
            ProcessPath(request.From, request.To,
                (source, dest) => System.IO.File.Move(source, dest, true),
                (source, dest) => Directory.Move(source, dest));
            return Ok();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("copy")]
    public IActionResult Copy([FromBody] PathRequest request)
    {
        try
        {
            ProcessPath(request.From, request.To,
                (source, dest) => System.IO.File.Copy(source, dest, true),
                (source, dest) => CopyDirectory(source, dest));
            return Ok();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("zip")]
    public async Task<IActionResult> Zip([FromBody] PathsRequest request)
    {
        if (request.Paths == null || request.Paths.Count == 0)
            return BadRequest("No paths supplied");
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var relative in request.Paths)
            {
                var full = _pathResolver.Resolve(relative);
                await AddToArchive(archive, full);
            }
        }
        ms.Position = 0;
        return File(ms, "application/zip", "files.zip");
    }

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string query)
    {
        var files = new List<FoundFile>();
        var directories = new List<FoundDirectory>();

        foreach (var path in Directory.EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            if (!name.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            var attributes = System.IO.File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
            {
                directories.Add(new FoundDirectory(Path.GetRelativePath(_root, path)));
            }
            else
            {
                files.Add(new FoundFile(Path.GetRelativePath(_root, path), new FileInfo(path).Length));
            }
        }

        return Ok(new SearchResult(files, directories));
    }

    private void ProcessPath(string from, string to, Action<string, string> fileAction, Action<string, string> dirAction)
    {
        var source = _pathResolver.Resolve(from);
        var dest = _pathResolver.Resolve(to);
        if (System.IO.File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            fileAction(source, dest);
        }
        else if (Directory.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            dirAction(source, dest);
        }
        else
        {
            throw new FileNotFoundException();
        }
    }

    private async Task AddToArchive(ZipArchive archive, string fullPath)
    {
        if (System.IO.File.Exists(fullPath))
        {
            var entryPath = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
            var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
            await using var src = System.IO.File.OpenRead(fullPath);
            await using var dest = entry.Open();
            await src.CopyToAsync(dest);
        }
        else if (Directory.Exists(fullPath))
        {
            foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
            {
                var entryPath = Path.GetRelativePath(_root, file).Replace('\\', '/');
                var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
                await using var src = System.IO.File.OpenRead(file);
                await using var dest = entry.Open();
                await src.CopyToAsync(dest);
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFilePath = Path.Combine(destDir, Path.GetFileName(file));
            System.IO.File.Copy(file, targetFilePath, true);
        }
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var targetDirPath = Path.Combine(destDir, Path.GetFileName(directory));
            CopyDirectory(directory, targetDirPath);
        }
    }
}
