using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using TestProject.Services;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace TestProject.Controllers;

[ApiController]
[Route("api/preview")]
public class PreviewController : ControllerBase
{
    private readonly PathResolver _pathResolver;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public PreviewController(PathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string path)
    {
        var full = _pathResolver.Resolve(path);
        if (!System.IO.File.Exists(full))
            return NotFound();

        var ext = Path.GetExtension(full).ToLowerInvariant();
        if (ext == ".kml")
        {
            var kml = System.IO.File.ReadAllText(full);
            var geojson = KmlToGeoJson(kml);
            return Content(geojson, "application/geo+json");
        }

        if (!_contentTypeProvider.TryGetContentType(full, out var contentType))
            contentType = "application/octet-stream";

        return PhysicalFile(full, contentType);
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
}
