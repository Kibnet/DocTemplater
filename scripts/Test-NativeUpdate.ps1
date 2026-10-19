param(
    [Parameter(Mandatory)][string]$StateFile,
    [Parameter(Mandatory)][string]$NewReleaseDirectory,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$driver = Join-Path $PSScriptRoot 'NativeSmoke/bin/Release/net10.0-windows/NativeSmoke.exe'
$state = Get-Content -LiteralPath $StateFile -Raw | ConvertFrom-Json
$env:DOCTEMPLATER_FIXTURE_DATA = $state.Data
$env:DOCTEMPLATER_FIXTURE_FEED = $state.Feed
$name = 'DocTemplater.UpdateFixture'
$exe = Join-Path $state.Install 'current/DocTemplater.UpdateFixture.exe'
$null = New-Item -ItemType Directory -Path $EvidenceDirectory -Force
function Drive([string]$Action, [string]$Stage) {
    $stageDirectory = Join-Path $EvidenceDirectory $Stage
    & $driver $name $Action $stageDirectory
    if ($LASTEXITCODE) { throw "Native driver failed: $Action / $Stage" }
}
function Wait-Exit([int]$ProcessId) {
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($process -and -not $process.WaitForExit(15000)) { throw "Process did not exit: $ProcessId" }
}
$files = @((Join-Path $state.Data 'settings.json'), (Join-Path $state.Templates 'Contract.docx'), (Join-Path $state.Output 'existing.docx'))
$oldProcess = Get-Process $name | Where-Object Path -eq $exe | Select-Object -First 1
if (-not $oldProcess) { throw 'Fixture process not found.' }
$beforeExeHash = (Get-FileHash -LiteralPath $exe).Hash
$beforeDllHash = (Get-FileHash -LiteralPath (Join-Path $state.Install 'current/DocTemplater.UpdateFixture.dll')).Hash
Drive select dirty
Drive inspect before-second-process
$before = @($files | Get-FileHash) # Bootstrap and saved-value load have completed before baseline capture.
$before | ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'before-hashes.json')
$second = Start-Process -FilePath $exe -PassThru
if (-not $second.WaitForExit(12000) -or $second.ExitCode -ne 1) { throw 'Second process was not rejected.' }
if ((Get-Process -Id $oldProcess.Id).HasExited) { throw 'First process was stopped.' }
foreach ($file in $before) { if ($file.Hash -ne (Get-FileHash -LiteralPath $file.Path).Hash) { throw 'Second instance changed profile.' } }
Copy-Item -LiteralPath (Join-Path $NewReleaseDirectory 'releases.win.json') -Destination (Join-Path $state.Feed 'releases.win.json') -Force
Get-ChildItem -LiteralPath $NewReleaseDirectory -Filter '*full.nupkg' | Copy-Item -Destination $state.Feed
Drive check check-new-feed
Drive ready ready
Drive cancel cancel-keeps-input
Drive close normal-close-with-pending
Wait-Exit $oldProcess.Id
Start-Process -FilePath $exe
Drive ready pending-after-restart
if ((Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'pending-after-restart/uia.txt') -Raw) -notmatch 'Версия 0.0.1') { throw 'Startup applied update without confirmation.' }
Drive select dirty-again
Drive confirm explicit-confirmation
$deadline = [DateTime]::UtcNow.AddSeconds(40)
do {
    Start-Sleep -Milliseconds 300
    $newProcess = Get-Process $name -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe -and $_.Id -ne $oldProcess.Id -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $manifest = if (Test-Path -LiteralPath (Join-Path $state.Install 'current/sq.version')) { Get-Content -LiteralPath (Join-Path $state.Install 'current/sq.version') -Raw } else { '' }
} until (($newProcess -and $manifest -match '<version>0.0.2</version>') -or [DateTime]::UtcNow -ge $deadline)
if (-not $newProcess -or $manifest -notmatch '<version>0.0.2</version>') { throw 'Native restart/version change failed.' }
Drive inspect updated
if ((Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'updated/uia.txt') -Raw) -notmatch 'Версия 0.0.2') { throw 'Updated UI version missing.' }
foreach ($file in $before) { if ($file.Hash -ne (Get-FileHash -LiteralPath $file.Path).Hash) { throw "Data changed: $($file.Path)" } }
$receipt = @{ BeforePid=$oldProcess.Id; AfterPid=$newProcess.Id; BeforeVersion='0.0.1'; AfterVersion='0.0.2'; Executable=$exe; BeforeExeSha256=$beforeExeHash; AfterExeSha256=(Get-FileHash -LiteralPath $exe).Hash; BeforeDllSha256=$beforeDllHash; AfterDllSha256=(Get-FileHash -LiteralPath (Join-Path $state.Install 'current/DocTemplater.UpdateFixture.dll')).Hash; DataHashesUnchanged=$true; SecondInstanceRejected=$true; DirtyCancelPreservedInput=$true; PendingRestartRequiresConfirmation=$true; DesktopCapture='Locked desktop: UIA + inspected Headless PNG fallback'; Timestamp=[DateTimeOffset]::Now.ToString('O') }
$receipt | ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'receipt.json')
Write-Output 'Native packaged application update PASS.'
