using Microsoft.AspNetCore.Http;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TestProject.Models;

namespace TestProject.Services;

public class FileService
{
    private readonly PathResolver _pathResolver;
    private readonly string _root;

    public FileService(PathResolver pathResolver)
    {
        _pathResolver = pathResolver;
        _root = _pathResolver.Resolve(null);
    }

    public DirectoryListing List(string? path)
    {
        var full = _pathResolver.Resolve(path);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException();
        var directory = new DirectoryInfo(full);
        var directories = directory.GetDirectories().Select(d => d.Name).ToList();
        var files = directory.GetFiles().Select(f => new FileItem(f.Name, f.Length)).ToList();
        var stats = new Stats(directories.Count, files.Count, files.Sum(f => f.Size));
        return new DirectoryListing(path ?? string.Empty, directories, files, stats);
    }

    public (string FullPath, string FileName) GetDownloadInfo(string path)
    {
        var full = _pathResolver.Resolve(path);
        if (!File.Exists(full))
            throw new FileNotFoundException();
        var fileName = Path.GetFileName(full);
        return (full, fileName);
    }

    public async Task Upload(string? path, IFormFile file)
    {
        var folder = _pathResolver.Resolve(path);
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException();
        var dest = Path.Combine(folder, Path.GetFileName(file.FileName));
        await using var stream = File.Create(dest);
        await file.CopyToAsync(stream);
    }

    public void CreateDirectory(string path)
    {
        var full = _pathResolver.Resolve(path);
        if (File.Exists(full))
            throw new IOException();
        Directory.CreateDirectory(full);
    }

    public void Delete(string path)
    {
        var full = _pathResolver.Resolve(path);
        if (File.Exists(full))
            File.Delete(full);
        else if (Directory.Exists(full))
            Directory.Delete(full, true);
        else
            throw new FileNotFoundException();
    }

    public void Move(string from, string to)
    {
        ProcessPath(from, to,
            (source, dest) => File.Move(source, dest, true),
            (source, dest) => Directory.Move(source, dest));
    }

    public void Copy(string from, string to)
    {
        ProcessPath(from, to,
            (source, dest) => File.Copy(source, dest, true),
            (source, dest) => CopyDirectory(source, dest));
    }

    public async Task<MemoryStream> Zip(List<string> paths)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var relative in paths)
            {
                var full = _pathResolver.Resolve(relative);
                await AddToArchive(archive, full);
            }
        }
        ms.Position = 0;
        return ms;
    }

    public SearchResult Search(string query)
    {
        var files = new List<FoundFile>();
        var directories = new List<FoundDirectory>();

        foreach (var path in Directory.EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            if (!name.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
            {
                directories.Add(new FoundDirectory(Path.GetRelativePath(_root, path)));
            }
            else
            {
                files.Add(new FoundFile(Path.GetRelativePath(_root, path), new FileInfo(path).Length));
            }
        }

        return new SearchResult(files, directories);
    }

    private void ProcessPath(string from, string to, Action<string, string> fileAction, Action<string, string> dirAction)
    {
        var source = _pathResolver.Resolve(from);
        var dest = _pathResolver.Resolve(to);
        if (File.Exists(source))
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

    private async Task AddToArchive(ZipArchive archive, string fullPath)
    {
        if (File.Exists(fullPath))
        {
            var entryPath = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
            var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
            await using var src = File.OpenRead(fullPath);
            await using var dest = entry.Open();
            await src.CopyToAsync(dest);
        }
        else if (Directory.Exists(fullPath))
        {
            foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
            {
                var entryPath = Path.GetRelativePath(_root, file).Replace('\\', '/');
                var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
                await using var src = File.OpenRead(file);
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
            File.Copy(file, targetFilePath, true);
        }
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var targetDirPath = Path.Combine(destDir, Path.GetFileName(directory));
            CopyDirectory(directory, targetDirPath);
        }
    }
}
