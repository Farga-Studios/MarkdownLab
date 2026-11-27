<p align="center"><img src="MarkdownLab/Assets/MarkdownLab.svg" width="96" alt="MarkdownLab logo"></p>

# MarkdownLab

A lightweight, modern Markdown reader and editor for Windows. Developed by **Sealsworn**.

[Español](README.es.md) · [Releases](https://github.com/Sealsworn/MarkdownLab/releases) · [User guide](docs/MANUAL.en.md) · [Changelog](CHANGELOG.md)

![MIT](https://img.shields.io/badge/license-MIT-blue) ![Windows](https://img.shields.io/badge/Windows-x64-blue) ![Build](https://github.com/Sealsworn/MarkdownLab/actions/workflows/windows.yml/badge.svg)

## Download

Visit [GitHub Releases](https://github.com/Sealsworn/MarkdownLab/releases/latest) for the Windows x64 installer or portable ZIP. Microsoft Edge WebView2 Runtime is required; .NET is bundled. The English interface is introduced in 1.7.0; the current public release may be older.

## Features

- Read, edit and split view with live preview, outline, zoom and syntax highlighting.
- Multiple tabs, recent files, folder browsing, find and replace.
- Formatting toolbar, tables, images, links, lists and tasks.
- Optional autosave, recovery snapshots and session restoration.
- Light and dark themes, focus mode, spelling suggestions using Windows dictionaries.
- HTML and PDF export; local image support.
- Single-instance file opening and optional Windows file association.
- GitHub release update checks with SHA-256 installer verification.
- Spanish and English interface in 1.7.0; spelling language is independent.

## Build

On Windows with .NET 8 SDK:

```powershell
dotnet run --project MarkdownLab/MarkdownLab.csproj
dotnet publish MarkdownLab/MarkdownLab.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

The installer uses Inno Setup. CI builds both packages.

## License

[MIT](LICENSE). Built with WPF, AvalonEdit, Markdig and WebView2. Only open documents from trusted sources; raw HTML is disabled in preview.
