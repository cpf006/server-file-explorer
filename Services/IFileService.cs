using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TestProject.Models;

namespace TestProject.Services;

public interface IFileService
{
    DirectoryListing List(string? path);
    void Delete(string path);
    void Move(string from, string to);
    void Copy(string from, string to);
    Task<Stream> Zip(List<string> paths);
    SearchResult Search(string query);
}

public record FileItem(string Name, long Size);
public record Stats(int DirectoryCount, int FileCount, long TotalSize);
public record DirectoryListing(string Path, List<string> Directories, List<FileItem> Files, Stats Stats);
public record FileResult(string Path, long Size);
public record DirectoryResult(string Path);
public record SearchResult(IEnumerable<FileResult> Files, IEnumerable<DirectoryResult> Directories);
