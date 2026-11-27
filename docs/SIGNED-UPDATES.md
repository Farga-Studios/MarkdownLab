# Signed updates — MarkdownLab

The 1.7.0 bridge must retain the existing installer AppId and the legacy asset MarkdownLab-Setup-win-x64.exe. Verify that the installed 1.6.1 can discover the new repository and update before public rollout; do not assume repository redirects satisfy its URL validation. Both installer names must contain identical bytes. Never publish 1.7.0 before owner approval.

## Signing
Use PowerShell 7 and a restricted encrypted directory outside all repositories. Run scripts/New-UpdateKey.ps1 -PrivateKeyPath <external path>. Commit only MarkdownLab/Updates/PublicKey.pem and rebuild. Never share or commit the private key. Back it up securely. Without the embedded key, automatic updates are disabled.

Build and test the versioned installer on Windows. Run scripts/New-UpdateManifest.ps1 with Version, Installer, PrivateKeyPath, NotesPath and OutputDirectory. Upload the exact tested assets to release vX.Y.Z. Verify anonymous availability and SHA-256 before committing update.json and update.json.sig together in channels/stable. Never replace released assets. No unsigned fallback. Authenticode is separate.

## Acceptance gates
- Clean Windows CI restore and build with .NET 8 and public NuGet package 0.2.1.
- Legacy 1.6.1 update discovery, installer identity, settings and file associations.
- Signed baseline to newer signed version; save, cancel, process exit, optional restart.
- Reject altered manifest, wrong key, altered installer, wrong size and wrong product.
- Offline behavior, missing key, portable manual download.
- Verify open documents, session restoration and preferences after installation.

Automatic rollback is not implemented. Retain a verified previous installer and back up user data. Manual Windows acceptance remains mandatory.
