param([Parameter(Mandatory)][string]$InstallerPath, [Parameter(Mandatory)][string]$FeedPath, [Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('DocTemplater-Native-' + [guid]::NewGuid().ToString('N'))
$data = Join-Path $root 'data'
$templates = Join-Path $data 'Templates'
$output = Join-Path $root 'documents'
$install = Join-Path $root 'install'
$null = New-Item -ItemType Directory -Path $templates,$output,$EvidenceDirectory -Force
$document = Join-Path $templates 'Contract.docx'
$zip = [IO.Compression.ZipFile]::Open($document, [IO.Compression.ZipArchiveMode]::Create)
try {
    $parts = @{
        '[Content_Types].xml' = '<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>'
        '_rels/.rels' = '<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'
        'word/document.xml' = '<?xml version="1.0" encoding="utf-8"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Contract {Customer:String()}</w:t></w:r></w:p></w:body></w:document>'
    }
    foreach ($part in $parts.GetEnumerator()) {
        $entry = $zip.CreateEntry($part.Key)
        $writer = [IO.StreamWriter]::new($entry.Open(), [Text.UTF8Encoding]::new($false))
        try { $writer.Write($part.Value) } finally { $writer.Dispose() }
    }
} finally { $zip.Dispose() }
$settings = @{
    TemplatesFolderPath=$templates; DefaultOutputPath=$output; CultureName='ru-RU'; OpenAfterSaveDefault=$false
    Templates=@(@{ TemplateFileName='Contract.docx'; FieldValues=@{Customer='Saved fixture value'}; LastUsed='2026-10-20T00:00:00+03:00'; LastSaveDirectory=$output; OutputFileNameTemplate='Contract_filled' })
}
[IO.File]::WriteAllText((Join-Path $data 'settings.json'), ($settings | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $output 'existing.docx'), 'Existing generated document')
$state = @{Root=$root; Data=$data; Templates=$templates; Output=$output; Install=$install; Feed=[IO.Path]::GetFullPath($FeedPath)}
$state | ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'state.json')
$env:DOCTEMPLATER_FIXTURE_DATA=$data
$env:DOCTEMPLATER_FIXTURE_FEED=$state.Feed
$setup = Start-Process -FilePath ([IO.Path]::GetFullPath($InstallerPath)) -ArgumentList @('--silent', '--installto', $install, '--log', (Join-Path $EvidenceDirectory 'setup.log')) -WindowStyle Hidden -PassThru -Wait
if ($setup.ExitCode) { throw "Installer exit: $($setup.ExitCode)" }
$installedExe = Join-Path $install 'current/DocTemplater.UpdateFixture.exe'
Start-Sleep -Milliseconds 1500
if (-not (Get-Process 'DocTemplater.UpdateFixture' -ErrorAction SilentlyContinue | Where-Object Path -eq $installedExe)) {
    Start-Process -FilePath $installedExe
}
Write-Output "Fixture installed: $install"
