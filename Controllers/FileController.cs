using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

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

    [HttpDelete]
    public IActionResult Delete([FromQuery] string path)
    {
        var full = ResolvePath(path);
        if (System.IO.File.Exists(full))
            System.IO.File.Delete(full);
        else if (Directory.Exists(full))
            Directory.Delete(full, true);
        else
            return NotFound();

        return Ok();
    }

    public record PathRequest(string From, string To);

    public record PathsRequest(List<string> Paths);

    [HttpPost("move")]
    public IActionResult Move([FromBody] PathRequest request)
    {
        var source = ResolvePath(request.From);
        var dest = ResolvePath(request.To);

        if (System.IO.File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            System.IO.File.Move(source, dest, true);
        }
        else if (Directory.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            Directory.Move(source, dest);
        }
        else
        {
            return NotFound();
        }

        return Ok();
    }

    [HttpPost("copy")]
    public IActionResult Copy([FromBody] PathRequest request)
    {
        var source = ResolvePath(request.From);
        var dest = ResolvePath(request.To);

        if (System.IO.File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            System.IO.File.Copy(source, dest, true);
        }
        else if (Directory.Exists(source))
        {
            CopyDirectory(source, dest);
        }
        else
        {
            return NotFound();
        }

        return Ok();
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
                var full = ResolvePath(relative);
                await AddToArchive(archive, full);
            }
        }
        ms.Position = 0;
        return File(ms, "application/zip", "files.zip");
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

    [HttpGet("geo-preview")]
    public IActionResult GeoPreview([FromQuery] string path)
    {
        var full = ResolvePath(path);
        if (!System.IO.File.Exists(full))
            return NotFound();

        var ext = Path.GetExtension(full).ToLowerInvariant();
        string geojson;
        if (ext == ".geojson" || ext == ".json")
        {
            geojson = System.IO.File.ReadAllText(full);
        }
        else if (ext == ".kml")
        {
            var kml = System.IO.File.ReadAllText(full);
            geojson = KmlToGeoJson(kml);
        }
        else
        {
            return BadRequest("Unsupported file type");
        }

        return Content(geojson, "application/geo+json");
    }

    private static string KmlToGeoJson(string kmlContent)
    {
        var doc = XDocument.Parse(kmlContent);
        XNamespace ns = doc.Root!.Name.Namespace;
        var features = new List<object>();

        static double[] ParseLonLat(string coord)
        {
            var parts = coord.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var lon = double.Parse(parts[0], CultureInfo.InvariantCulture);
            var lat = double.Parse(parts[1], CultureInfo.InvariantCulture);
            return new[] { lon, lat };
        }

        static double[][] ParseCoordinateList(string coordText) =>
            coordText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(ParseLonLat)
                     .ToArray();

        foreach (var pm in doc.Descendants(ns + "Placemark"))
        {
            var name = pm.Element(ns + "name")?.Value;
            object? geometry = null;

            var point = pm.Element(ns + "Point");
            if (point != null)
            {
                var coordText = point.Element(ns + "coordinates")?.Value.Trim();
                if (!string.IsNullOrEmpty(coordText))
                {
                    geometry = new { type = "Point", coordinates = ParseLonLat(coordText) };
                }
            }

            var line = pm.Element(ns + "LineString");
            if (geometry == null && line != null)
            {
                var coordText = line.Element(ns + "coordinates")?.Value;
                if (!string.IsNullOrEmpty(coordText))
                {
                    geometry = new { type = "LineString", coordinates = ParseCoordinateList(coordText) };
                }
            }

            var poly = pm.Element(ns + "Polygon");
            if (geometry == null && poly != null)
            {
                var coordText = poly.Descendants(ns + "outerBoundaryIs").Descendants(ns + "coordinates").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(coordText))
                {
                    var coords = ParseCoordinateList(coordText);
                    geometry = new { type = "Polygon", coordinates = new[] { coords } };
                }
            }

            if (geometry != null)
            {
                features.Add(new { type = "Feature", properties = new { name }, geometry });
            }
        }

        var fc = new { type = "FeatureCollection", features };
        return JsonSerializer.Serialize(fc);
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

