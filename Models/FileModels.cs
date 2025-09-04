using System.Collections.Generic;

namespace TestProject.Models;

public record FileItem(string Name, long Size);
public record Stats(int DirectoryCount, int FileCount, long TotalSize);
public record DirectoryListing(string Path, List<string> Directories, List<FileItem> Files, Stats Stats);
public record FoundFile(string Path, long Size);
public record FoundDirectory(string Path);
public record SearchResult(IEnumerable<FoundFile> Files, IEnumerable<FoundDirectory> Directories);
