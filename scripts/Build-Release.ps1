param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$OutputDirectory = 'artifacts/release',
    [switch]$NativeUpdateFixture
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repoRoot
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $outputRoot.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be inside repository artifacts/.' }
if (Test-Path -LiteralPath $outputRoot) { throw "Output already exists: $outputRoot. Choose a fresh directory." }
$null = New-Item -ItemType Directory -Path $outputRoot
$publishPath = Join-Path $outputRoot 'publish'
$releasePath = Join-Path $outputRoot 'releases'
$packId = if ($NativeUpdateFixture) { 'DocTemplater.UpdateFixture' } else { 'DocTemplater' }
$notes = Join-Path $repoRoot 'releases/0.1.0.md'
dotnet tool restore
if ($LASTEXITCODE) { throw 'Tool restore failed.' }
dotnet publish DocTemplater/DocTemplater.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false "-p:Version=$Version" "-p:NativeUpdateFixture=$($NativeUpdateFixture.IsPresent.ToString().ToLowerInvariant())" -o $publishPath
if ($LASTEXITCODE) { throw 'Publish failed.' }
if (-not $NativeUpdateFixture) {
    if ((Get-Content DocTemplater/DocTemplater.csproj -Raw) -notmatch "<Version>$([regex]::Escape($Version))</Version>") { throw 'Version does not match csproj.' }
    if (-not (Test-Path -LiteralPath "releases/$Version.md")) { throw 'Release notes are missing.' }
    $notes = Join-Path $repoRoot "releases/$Version.md"
}
dotnet tool run vpk -- pack --packId $packId --packVersion $Version --packDir $publishPath --mainExe "$packId.exe" --runtime win-x64 --channel win --outputDir $releasePath --releaseNotes $notes --icon DocTemplater/Assets/avalonia-logo.ico
if ($LASTEXITCODE) { throw 'Velopack pack failed.' }
$required = @("$packId-win-Setup.exe", "$packId-$Version-full.nupkg", 'releases.win.json')
foreach ($asset in $required) { if (-not (Test-Path -LiteralPath (Join-Path $releasePath $asset))) { throw "Missing asset $asset" } }
$manifest = Get-ChildItem -LiteralPath $releasePath -File | Sort-Object Name | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
[IO.File]::WriteAllLines((Join-Path $releasePath 'SHA256SUMS.txt'), $manifest, [Text.UTF8Encoding]::new($false))
Write-Output "Release assets: $releasePath"
