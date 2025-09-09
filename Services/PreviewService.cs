using Microsoft.AspNetCore.StaticFiles;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace TestProject.Services;

public class PreviewService
{
    private readonly PathResolver _pathResolver;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public PreviewService(PathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    public PreviewResult GetPreview(string path)
    {
        var full = _pathResolver.Resolve(path);
        if (!File.Exists(full))
            throw new FileNotFoundException();

        var ext = Path.GetExtension(full).ToLowerInvariant();
        if (ext == ".kml")
        {
            var kml = File.ReadAllText(full);
            var geojson = KmlToGeoJson(kml);
            return PreviewResult.FromContent(geojson, "application/geo+json");
        }

        if (!_contentTypeProvider.TryGetContentType(full, out var contentType))
            contentType = "application/octet-stream";

        return PreviewResult.FromFile(full, contentType);
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

public record PreviewResult(string? FilePath, string? Content, string ContentType)
{
    public static PreviewResult FromFile(string path, string contentType) => new(path, null, contentType);
    public static PreviewResult FromContent(string content, string contentType) => new(null, content, contentType);
}
