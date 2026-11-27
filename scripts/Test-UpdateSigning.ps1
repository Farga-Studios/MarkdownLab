$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ("MarkdownLab-signing-" + [Guid]::NewGuid().ToString('N'))
try {
    $repo = Join-Path $root 'repo'
    $scripts = Join-Path $repo 'scripts'
    New-Item -ItemType Directory -Force $scripts | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'New-UpdateKey.ps1'), (Join-Path $PSScriptRoot 'New-UpdateManifest.ps1') $scripts
    $private = Join-Path $root 'test-private.pem'
    & (Join-Path $scripts 'New-UpdateKey.ps1') -PrivateKeyPath $private
    $installer = Join-Path $root 'MarkdownLab-1.7.1-win-x64-setup.exe'
    [IO.File]::WriteAllBytes($installer, [byte[]](1,2,3,4))
    $notes = Join-Path $root 'notes.txt'
    [IO.File]::WriteAllText($notes, 'Test only')
    $out = Join-Path $root 'out'
    $argsMap = @{Version='1.7.1';Installer=$installer;PrivateKeyPath=$private;NotesPath=$notes;OutputDirectory=$out}
    & (Join-Path $scripts 'New-UpdateManifest.ps1') @argsMap
    $data = [IO.File]::ReadAllBytes((Join-Path $out 'update.json'))
    $sig = [IO.File]::ReadAllBytes((Join-Path $out 'update.json.sig'))
    $rsa = [Security.Cryptography.RSA]::Create()
    try {
        $rsa.ImportFromPem([IO.File]::ReadAllText((Join-Path $repo 'MarkdownLab/Updates/PublicKey.pem')))
        $hash = [Security.Cryptography.HashAlgorithmName]::SHA256
        $padding = [Security.Cryptography.RSASignaturePadding]::Pkcs1
        if (-not $rsa.VerifyData($data,$sig,$hash,$padding)) { throw 'Valid signature rejected' }
        $manifest = [Text.Encoding]::UTF8.GetString($data) | ConvertFrom-Json
        if ($manifest.product -cne 'MarkdownLab' -or $manifest.version -cne '1.7.1' -or $manifest.bytes -ne 4 -or $manifest.sha256 -ne (Get-FileHash $installer).Hash) { throw 'Manifest mismatch' }
        $data[0] = $data[0] -bxor 1
        if ($rsa.VerifyData($data,$sig,$hash,$padding)) { throw 'Tampered manifest accepted' }
        $failed = $false
        try { & (Join-Path $scripts 'New-UpdateKey.ps1') -PrivateKeyPath $private } catch { $failed = $true }
        if (-not $failed) { throw 'Key overwrite accepted' }
    } finally { $rsa.Dispose() }
    Write-Host 'Signing smoke tests passed.'
} finally {
    if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
