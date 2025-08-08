# Git Configuration Setup for Cross-Platform Development

This document explains how to configure Git for consistent line endings across different operating systems.

**TL;DR**: We use LF line endings for ALL files in the repository. This is the modern standard and works perfectly on all platforms including Windows.

## Automatic Setup (Recommended)

Run the appropriate command for your operating system:

### Windows (PowerShell)

```powershell
git config --global core.autocrlf false
git config --global core.eol lf
```

### macOS/Linux (Bash)

```bash
git config --global core.autocrlf false
git config --global core.eol lf
```

## Manual Configuration

Alternatively, you can manually configure Git by adding these lines to your global `.gitconfig` file:

```ini
[core]
    autocrlf = false
    eol = lf
```

## Why LF for Everything?

- **Universal**: Works on Windows, macOS, and Linux
- **Standard**: Git, GitHub, and most tools expect LF
- **Smaller**: LF is 1 byte vs CRLF's 2 bytes
- **No Conversion**: Eliminates line ending issues completely
- **Modern**: All modern Windows tools handle LF perfectly

## What This Does

- **`.gitattributes`**: Forces LF line endings for all text files in the repository
- **`.editorconfig`**: Configures your editor to use LF line endings
- **Git config**: Disables automatic conversion, uses LF everywhere

## Troubleshooting

If you still see line ending issues after setup:

1. **Reset line endings in existing files:**

   ```bash
   git rm --cached -r .
   git reset --hard
   ```

2. **Check current Git configuration:**

   ```bash
   git config --list | grep -E "(autocrlf|eol)"
   ```

3. **Verify file line endings:**

   ```bash
   file -b path/to/file.cs
   ```
