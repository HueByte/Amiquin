#!/usr/bin/env pwsh
# Line Ending Fix Script
# This script converts CRLF line endings to LF in text files

Write-Host "🔧 Converting CRLF to LF in text files..." -ForegroundColor Yellow

$extensions = @("*.cs", "*.json", "*.yml", "*.yaml", "*.xml", "*.md", "*.txt", "*.ps1", "*.sh", "*.cmd")
$fixed_count = 0

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
            $content = $content -replace "`r`n", "`n"
            Set-Content $file.FullName -Value $content -NoNewline
            $fixed_count++
            Write-Host "  ✓ Fixed: $($file.FullName)" -ForegroundColor Green
        }
    }
}

if ($fixed_count -eq 0) {
    Write-Host "✅ No files needed fixing - all already have LF line endings!" -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "✅ Fixed $fixed_count files with CRLF line endings!" -ForegroundColor Green
    Write-Host "💡 Remember to commit these changes:" -ForegroundColor Yellow
    Write-Host "   git add ." -ForegroundColor Cyan
    Write-Host "   git commit -m 'Fix line endings to LF'" -ForegroundColor Cyan
}
