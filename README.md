# Server File Explorer

An end‑to‑end .NET 6 project that exposes a JSON Web API and a single‑page app for browsing files on the server.

## Setup
Requires the .NET 6 SDK.

```bash
dotnet run
```

The app listens on http://localhost:5000 and https://localhost:5001. On first run it creates a `DefaultDirectory` with a placeholder file so project sources stay isolated.

Run tests with:

```bash
dotnet test
```

## Project requirements covered
- Web API that browses and searches files and folders
- Single page app with deep‑linkable URLs (`?path=...`)
- Upload and download files from the browser
- Show file and folder counts and total size for the current view

## Additional features
- Preview text, images and geospatial files (KML converted to GeoJSON) in a dialog
- Create directories
- Delete files or folders
- Move or copy files and folders
- Download multiple selections as a ZIP archive

## Configuration
The root folder for all file operations is `FileExplorer:RootPath` in *appsettings.json* or the `FileExplorer__RootPath` environment variable. By default it points to the `DefaultDirectory` folder.
