using System.Collections.Generic;

namespace TestProject.Models;

public record PathRequest(string From, string To);

public record PathsRequest(List<string> Paths);
