using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using TestProject.Models;

namespace TestProject.Services;

public class FileService : IFileService
{
    private readonly PathResolver _resolver;

    public FileService(PathResolver resolver)
    {
        _resolver = resolver;
    }

    public DirectoryListing List(string? path)
    {
        var full = _resolver.ResolvePath(path);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException();

        var directory = new DirectoryInfo(full);
        var directories = directory.GetDirectories().Select(d => d.Name).ToList();
        var files = directory.GetFiles().Select(f => new FileItem(f.Name, f.Length)).ToList();
        var stats = new Stats(directories.Count, files.Count, files.Sum(f => f.Size));
        return new DirectoryListing(path ?? string.Empty, directories, files, stats);
    }

    public void Delete(string path)
    {
        var full = _resolver.ResolvePath(path);
        if (System.IO.File.Exists(full))
            System.IO.File.Delete(full);
        else if (Directory.Exists(full))
            Directory.Delete(full, true);
        else
            throw new FileNotFoundException();
    }

    public void Move(string from, string to)
    {
        ProcessPath(from, to,
            (source, dest) => System.IO.File.Move(source, dest, true),
            (source, dest) => Directory.Move(source, dest));
    }

    public void Copy(string from, string to)
    {
        ProcessPath(from, to,
            (source, dest) => System.IO.File.Copy(source, dest, true),
            (source, dest) => CopyDirectory(source, dest));
    }

    private void ProcessPath(string from, string to, Action<string, string> fileAction, Action<string, string> dirAction)
    {
        var source = _resolver.ResolvePath(from);
        var dest = _resolver.ResolvePath(to);

        if (System.IO.File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            fileAction(source, dest);
        }
        else if (Directory.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            dirAction(source, dest);
        }
        else
        {
            throw new FileNotFoundException();
        }
    }

    public async Task<Stream> Zip(List<string> paths)
    {
        if (paths == null || paths.Count == 0)
            throw new ArgumentException("No paths supplied", nameof(paths));

        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var relative in paths)
            {
                var full = _resolver.ResolvePath(relative);
                await AddToArchive(archive, full);
            }
        }
        ms.Position = 0;
        return ms;
    }

    public SearchResult Search(string query)
    {
        var root = _resolver.RootPath;
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(p => new FileResult(Path.GetRelativePath(root, p), new FileInfo(p).Length));
        var directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(p => new DirectoryResult(Path.GetRelativePath(root, p)));
        return new SearchResult(files, directories);
    }

    private async Task AddToArchive(ZipArchive archive, string fullPath)
    {
        if (System.IO.File.Exists(fullPath))
        {
            var entryPath = Path.GetRelativePath(_resolver.RootPath, fullPath).Replace('\\', '/');
            var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
            await using var src = System.IO.File.OpenRead(fullPath);
            await using var dest = entry.Open();
            await src.CopyToAsync(dest);
        }
        else if (Directory.Exists(fullPath))
        {
            foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
            {
                var entryPath = Path.GetRelativePath(_resolver.RootPath, file).Replace('\\', '/');
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
}
