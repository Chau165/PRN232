$ErrorActionPreference = 'Stop'
$srsPath = Join-Path $PSScriptRoot 'SRS_HeThongThuePhongTro_v1.0.docx'
$qaPath = Join-Path $PSScriptRoot 'qa'
New-Item -ItemType Directory -Path $qaPath -Force | Out-Null
$pdfPath = Join-Path $qaPath 'SRS_HeThongThuePhongTro_v1.0.pdf'
$word = $null
$document = $null
$pdfCreated = $false
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $word.AutomationSecurity = 3
    $document = $word.Documents.Open($srsPath, $false, $true, $false)
    $document.Repaginate()
    $document.Fields.Update() | Out-Null
    $document.ExportAsFixedFormat($pdfPath, 17)
    $pdfCreated = $true
    Write-Output "Rendered PDF: $pdfPath"
    Write-Output "Pages: $($document.ComputeStatistics(2))"
}
finally {
    if ($null -ne $document) {
        try { $document.Close(0) } catch { if (-not $pdfCreated) { throw } }
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($document)
    }
    if ($null -ne $word) {
        try { $word.Quit() } catch { if (-not $pdfCreated) { throw } }
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($word)
    }
}
