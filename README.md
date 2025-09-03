# Server File Explorer

A minimal .NET 9 project that exposes an API and single-page app for browsing files on the server.

## Features
- List directories and files with counts and sizes
- Upload and download files
- Search within the configured home directory

## Running
```
dotnet run
```
The app listens on http://localhost:5000 and https://localhost:5001. On first run it creates a `DefaultDirectory` with a placeholder file so project sources stay isolated.

## Testing
```
dotnet test
```

## Configuration
The root folder for all file operations is `FileExplorer:RootPath` in *appsettings.json* or the `FileExplorer__RootPath` environment variable. By default it points to the `DefaultDirectory` folder.
