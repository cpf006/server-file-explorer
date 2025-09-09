using Microsoft.AspNetCore.Mvc;
using System.IO;
using TestProject.Services;

namespace TestProject.Controllers;

[ApiController]
[Route("api/preview")]
public class PreviewController : ControllerBase
{
    private readonly PreviewService _previewService;

    public PreviewController(PreviewService previewService)
    {
        _previewService = previewService;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string path)
    {
        try
        {
            var result = _previewService.GetPreview(path);
            if (result.FilePath != null)
                return PhysicalFile(result.FilePath, result.ContentType);
            return Content(result.Content!, result.ContentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}
