using Microsoft.AspNetCore.Mvc;
using System.IO;
using TestProject.Services;
using TestProject.Models;

namespace TestProject.Controllers;

// Web API for browsing and manipulating files within a configured root directory.
[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
    private readonly FileService _fileService;

    public FileController(FileService fileService)
    {
        _fileService = fileService;
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
        try
        {
            var info = _fileService.GetDownloadInfo(path);
            return PhysicalFile(info.FullPath, "application/octet-stream", info.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("upload")]
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Upload([FromQuery] string? path, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file supplied");

        try
        {
            await _fileService.Upload(path, file);
            return Ok();
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("mkdir")]
    public IActionResult CreateDirectory([FromQuery] string path)
    {
        try
        {
            _fileService.CreateDirectory(path);
            return Ok();
        }
        catch (IOException)
        {
            return Conflict();
        }
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
        if (request.Paths == null || request.Paths.Count == 0)
            return BadRequest("No paths supplied");
        var ms = await _fileService.Zip(request.Paths);
        return File(ms, "application/zip", "files.zip");
    }

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string query)
    {
        var result = _fileService.Search(query);
        return Ok(result);
    }
}
