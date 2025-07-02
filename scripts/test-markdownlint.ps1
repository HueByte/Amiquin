#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test script for markdownlint scripts

.DESCRIPTION
    This script tests both the PowerShell and Bash markdownlint scripts to ensure they work correctly.
#>

param(
    [switch]$SkipBash
)

$ErrorActionPreference = "Stop"

# Colors for output
$ErrorColor = "Red"
$SuccessColor = "Green"
$InfoColor = "Cyan"
$WarningColor = "Yellow"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

# Change to repository root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
Push-Location $repoRoot

try {
    Write-ColorOutput "🧪 Testing Markdownlint Scripts" $InfoColor
    Write-ColorOutput "==============================" $InfoColor
    Write-ColorOutput ""

    # Test PowerShell script
    Write-ColorOutput "📝 Testing PowerShell script..." $InfoColor
    try {
        & .\scripts\markdownlint.ps1 -Path "README.md"
        Write-ColorOutput "✅ PowerShell script test passed" $SuccessColor
    }
    catch {
        Write-ColorOutput "❌ PowerShell script test failed: $_" $ErrorColor
        throw
    }

    Write-ColorOutput ""

    # Test Bash script (if not skipped and bash is available)
    if (-not $SkipBash) {
        if (Get-Command bash -ErrorAction SilentlyContinue) {
            Write-ColorOutput "🐧 Testing Bash script..." $InfoColor
            try {
                bash .\scripts\markdownlint.sh README.md
                Write-ColorOutput "✅ Bash script test passed" $SuccessColor
            }
            catch {
                Write-ColorOutput "❌ Bash script test failed: $_" $ErrorColor
                throw
            }
        }
        else {
            Write-ColorOutput "⚠️ Bash not available, skipping Bash script test" $WarningColor
        }
    }
    else {
        Write-ColorOutput "⏭️ Skipping Bash script test" $InfoColor
    }

    Write-ColorOutput ""
    Write-ColorOutput "🎉 All markdownlint script tests passed!" $SuccessColor

}
finally {
    Pop-Location
}
