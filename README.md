# MarkdownLab

A lightweight Markdown reader and editor for Windows, featuring live preview, split view, dark mode, and document navigation.

## Features

- Read, edit, or split-screen Markdown with live preview
- Tables, task lists, fenced code, and images via Markdig
- Document outline, case-insensitive find, and zoom
- Light/dark preview and editor
- Open and save UTF-8 Markdown documents
- Drag and drop an `.md` file into the window
- Open a document from the command line

## Download

In [Actions](../../actions/workflows/windows.yml), open a successful **Windows build**, then download the `MarkdownLab-win-x64` artifact. Extract it and run `MarkdownLab.exe`.

## Build locally

Requires Visual Studio 2022 with .NET desktop development workload, or .NET 8 SDK on Windows.

```powershell
dotnet run --project MarkdownLab/MarkdownLab.csproj
```

To publish a portable, self-contained Windows executable:

```powershell
dotnet publish MarkdownLab/MarkdownLab.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Shortcuts

- `Ctrl+O`: Open
- `Ctrl+S`: Save
- Press Enter in search to find next occurrence

## Scope

MarkdownLab 1.1 focuses on reading and editing. PDF export, plugins, and cloud synchronization are intentionally out of scope.

## Security note

Only open Markdown files from sources you trust. The preview uses Microsoft Edge WebView2 Runtime (required on Windows). Raw HTML is disabled and navigation is blocked. Relative local images may be limited by WebView2 security policies.

## License

MIT.

## Version 1.1

Modern toolbar, welcome screen, resizable outline, AvalonEdit line numbers, document statistics and application icon.
