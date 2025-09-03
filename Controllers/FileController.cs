using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IO;

namespace TestProject.Controllers;

// Web API for browsing and manipulating files within a configured root directory.
[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
    private readonly string _root;
    private readonly ILogger<FileController> _logger;

    // Pull the root path from options and normalize it.
    public FileController(IOptions<FileExplorerOptions> options, ILogger<FileController> logger)
    {
        _root = Path.GetFullPath(options.Value.RootPath ?? Directory.GetCurrentDirectory());
        _logger = logger;
    }

    // Convert a user supplied path into one rooted under the configured directory.
    private string ResolvePath(string? relative)
    {
        relative ??= string.Empty;
        var combined = Path.GetFullPath(Path.Combine(_root, relative));
        if (!combined.StartsWith(_root))
            throw new InvalidOperationException("Invalid path");
        return combined;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string? path)
    {
        var full = ResolvePath(path);
        if (!Directory.Exists(full))
            return NotFound();

        var directory = new DirectoryInfo(full);
        var directories = directory.GetDirectories().Select(d => d.Name).ToList();
        var files = directory.GetFiles().Select(f => new { name = f.Name, size = f.Length }).ToList();

        var result = new
        {
            path = path ?? string.Empty,
            directories,
            files,
            stats = new
            {
                directoryCount = directories.Count,
                fileCount = files.Count,
                totalSize = files.Sum(f => f.size)
            }
        };

        return Ok(result);
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path)
    {
        var full = ResolvePath(path);
        if (!System.IO.File.Exists(full))
            return NotFound();
        var fileName = Path.GetFileName(full);
        return PhysicalFile(full, "application/octet-stream", fileName);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromQuery] string? path, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file supplied");

        var folder = ResolvePath(path);
        if (!Directory.Exists(folder))
            return NotFound();

        var dest = Path.Combine(folder, Path.GetFileName(file.FileName));
        await using var stream = System.IO.File.Create(dest);
        await file.CopyToAsync(stream);
        return Ok();
    }

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string query)
    {
        var files = Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(p => new { path = Path.GetRelativePath(_root, p), size = new FileInfo(p).Length });

        var directories = Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(p => new { path = Path.GetRelativePath(_root, p) });

        return Ok(new { files, directories });
    }
}

