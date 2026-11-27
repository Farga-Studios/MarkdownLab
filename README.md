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

[MIT](LICENSE). Developed by Sealsworn.

## Version 1.1

Modern toolbar, welcome screen, resizable outline, AvalonEdit line numbers, document statistics and application icon.

## Version 1.2

Save As (Ctrl+Shift+S), editor context menu, Ctrl+F, and Markdown formatting toolbar with syntax tooltips in edit and split modes.

## 1.3.0

Desktop menus, heading selector, smart lists, image picker (URL or local), visual table picker, Ctrl+B/I, Alt+Up/Down, word wrap, collapsible search, approximate editor-to-preview scroll sync. Local raster preview embeds images up to 750 KB each; WebView2 HTML size limits still apply.

## 1.3.1

About dialog with automatic assembly version, organization, license and repository link.

## 1.4.0

Document tabs, recent files, folder browsing, recovery snapshots, replace all, image drag/drop and file-based preview. Recovery snapshots every 15 seconds; original files are not overwritten automatically.

## 1.4.1

Restore NavigateToString preview; serve local raster images through a dedicated WebView2 virtual host and local image cache.

## 1.4.2

Save/discard/cancel prompts, save all, individual tab close, new tab button, atomic file replacement, configurable recovery and opt-in autosave, session restoration. Autosave only affects named documents.

## 1.5.0

Custom vector toolbar icons (project-owned geometry), themed menus and selection, HTML/PDF export, Spanish spelling review dialog, basic language-aware code highlighting, focus mode, persistent theme and zoom. No new third-party dependencies.

## 1.5.1

Custom window chrome, bordered tabs, themed heading picker, reorganized file menu, resizable split view and Markdown guide. Integrated spelling and further visual refinement remain pending.
