<p align="center"><img src="MarkdownLab/Assets/MarkdownLab.svg" width="96" alt="Logotipo MarkdownLab"></p>

# MarkdownLab

Lector y editor Markdown ligero y moderno para Windows. Desarrollado por **Farga Studios**.

[English](README.md) · [Descargas](https://github.com/Farga-Studios/MarkdownLab/releases/latest) · [Manual](docs/MANUAL.es.md) · [Historial](CHANGELOG.md)

![MIT](https://img.shields.io/badge/license-MIT-blue) ![Windows](https://img.shields.io/badge/Windows-x64-blue) ![Build](https://github.com/Farga-Studios/MarkdownLab/actions/workflows/windows.yml/badge.svg)

## Descargas

Instalador y ZIP portátil para Windows x64 en GitHub Releases. Requiere WebView2 Runtime; incluye .NET. La interfaz bilingüe se incorpora en 1.7.0; la última versión publicada puede ser anterior.

## Características

- Lectura, edición y vista dividida con previsualización, índice, zoom y resaltado.
- Pestañas, archivos recientes, carpetas, búsqueda y reemplazo.
- Barra de formato, tablas, imágenes, enlaces, listas y tareas.
- Autoguardado opcional, recuperación y restauración de sesión.
- Temas claro y oscuro, concentración, corrector ortográfico de Windows.
- Exportación HTML y PDF.
- Instancia única, asociación de archivos y actualizador con SHA-256.
- Interfaz en castellano e inglés desde 1.7.0.

## Desarrollo

En Windows con SDK .NET 8:

```powershell
dotnet run --project MarkdownLab/MarkdownLab.csproj
dotnet publish MarkdownLab/MarkdownLab.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

El instalador utiliza Inno Setup.

## Licencia

[MIT](LICENSE). WPF, AvalonEdit, Markdig y WebView2. Abre documentos de fuentes fiables.
