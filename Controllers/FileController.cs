using Microsoft.AspNetCore.Mvc;
using System.IO;
using TestProject.Models;
using TestProject.Services;

namespace TestProject.Controllers;

// Web API for browsing and manipulating files within a configured root directory.
[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
    private readonly PathResolver _resolver;
    private readonly ILogger<FileController> _logger;
    private readonly IFileService _fileService;

    // Pull the root path from options and normalize it via PathResolver.
    public FileController(PathResolver resolver, IFileService fileService, ILogger<FileController> logger)
    {
        _resolver = resolver;
        _fileService = fileService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string? path)
    {
        try
        {
            var result = _fileService.List(path);
            return Ok(result);
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path)
    {
        var full = _resolver.ResolvePath(path);
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

        var folder = _resolver.ResolvePath(path);
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
        var full = _resolver.ResolvePath(path);
        if (System.IO.File.Exists(full))
            return Conflict();
        Directory.CreateDirectory(full);
        return Ok();
    }

    [HttpDelete]
    public IActionResult Delete([FromQuery] string path)
    {
        try
        {
            _fileService.Delete(path);
            return Ok();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("move")]
    public IActionResult Move([FromBody] PathRequest request)
    {
        try
        {
            _fileService.Move(request.From, request.To);
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
            _fileService.Copy(request.From, request.To);
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
        try
        {
            var stream = await _fileService.Zip(request.Paths);
            return File(stream, "application/zip", "files.zip");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string query)
    {
        var result = _fileService.Search(query);
        return Ok(result);
    }
}

