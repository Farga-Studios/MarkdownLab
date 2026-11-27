# Changelog

## 1.7.0 — Internationalization (unreleased)

- Spanish and English interface, persistent language preference and bilingual installer.
- Localized recovery, settings, help and update dialogs.
- Bilingual documentation and refreshed repository README.

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

## 1.5.2

Shared themed application dialogs, custom dialog title bars, red close-button hover and improved Markdown syntax contrast. Native system file pickers and message boxes retain Windows styling.

## 1.5.3

Integrated spelling underlines and context suggestions using Windows WPF dictionaries (availability varies), categorized preferences, recent documents on welcome screen and vector welcome logo. Spelling is opt-in and skips common code/URL patterns; limited to 150,000 characters and 1,000 errors per pass.

## 1.6.0 — Stable candidate

Windows x64 installer and portable ZIP built by CI. [Manual en español](docs/MANUAL.es.md). Tagged builds prepare draft releases for review. WebView2 Runtime required.

Single-instance opening: double-clicking associated Markdown files forwards them to the running window, without duplicate tabs.

## 1.6.1

New flat MarkdownLab icon with nine ICO sizes, installer icon and document association; GitHub release update checker. Installer marker included.
