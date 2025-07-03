#!/usr/bin/env pwsh
# Line Ending Verification Script
# This script checks if any files have CRLF line endings

Write-Host "🔍 Checking for CRLF line endings in text files..." -ForegroundColor Yellow

$crlf_files = @()
$extensions = @("*.cs", "*.json", "*.yml", "*.yaml", "*.xml", "*.md", "*.txt", "*.ps1", "*.sh", "*.cmd")

foreach ($ext in $extensions) {
    $files = Get-ChildItem -Path . -Recurse -Include $ext | Where-Object { 
        -not $_.PSIsContainer -and 
        -not $_.FullName.Contains('\obj\') -and 
        -not $_.FullName.Contains('\bin\') -and
        -not $_.FullName.Contains('\generated\')
    }
    foreach ($file in $files) {
        $content = Get-Content $file.FullName -Raw
        if ($content -match "`r`n") {
            $crlf_files += $file.FullName
        }
    }
}

if ($crlf_files.Count -eq 0) {
    Write-Host "✅ All text files have LF line endings!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "❌ Files with CRLF line endings found:" -ForegroundColor Red
    foreach ($file in $crlf_files) {
        Write-Host "  - $file" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "💡 To fix these files, run:" -ForegroundColor Yellow
    Write-Host "   ./scripts/fix-line-endings.ps1" -ForegroundColor Cyan
    exit 1
}
