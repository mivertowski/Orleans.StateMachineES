# Package Signing Guide

This document provides instructions for signing the Orleans.StateMachineES NuGet packages.

## Prerequisites

- Code signing certificate (.pfx file)
- Certificate password
- Windows dotnet CLI (accessible from WSL)

## Quick Start

### From Windows (Recommended for Certificate Store)

If your certificate is in the Windows Certificate Store (smart card/USB token):

**PowerShell:**
```powershell
powershell -ExecutionPolicy Bypass -File .\sign-packages-certstore.ps1
```

Or simply **double-click** `sign-packages-certstore.ps1` in Windows Explorer.

### From WSL (Linux)

If using a certificate .pfx file:

```bash
./sign-packages.sh /path/to/certificate.pfx [password]
```

**Example:**
```bash
./sign-packages.sh ~/certificates/codesign.pfx mypassword
```

If your certificate doesn't have a password:
```bash
./sign-packages.sh ~/certificates/codesign.pfx
```

**Note:** If your certificate is in the Windows Certificate Store, the bash script will detect if WSL interop is not working and provide instructions to use the PowerShell script instead.

### Alternative: Windows PowerShell with .pfx File

If you prefer Windows PowerShell with a certificate file:

**With certificate file (.pfx):**
```powershell
.\sign-packages-dotnet.ps1 -CertificatePath "C:\path\to\cert.pfx" -CertificatePassword "password"
```

**With certificate from Windows Store:**
```powershell
# First install nuget.exe
Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "$env:LOCALAPPDATA\Microsoft\WindowsApps\nuget.exe"

# Then run the signing script
.\sign-packages-simple.ps1
```

## Available Scripts

### PowerShell Scripts (Windows)

1. **sign-packages-certstore.ps1** (Recommended for Certificate Store)
   - Uses dotnet CLI with Windows Certificate Store
   - Works with smart cards and USB tokens
   - Certificate thumbprint: D4CF73C16E699353F1D2222237D2250448850D2B
   - Interactive confirmation before signing
   - Automatic signature verification

2. **sign-packages-dotnet.ps1**
   - Uses dotnet CLI with certificate file
   - Works with .pfx files
   - Requires: certificate file and password

3. **sign-packages-simple.ps1**
   - Uses nuget.exe with Windows Certificate Store
   - Requires: nuget.exe installed, certificate in store
   - Certificate thumbprint: D4CF73C16E699353F1D2222237D2250448850D2B

4. **sign-packages.ps1**
   - Detailed version with extensive validation
   - Uses nuget.exe with Certificate Store

### Bash Scripts (WSL/Linux)

1. **sign-packages.sh**
   - Supports both certificate store and .pfx files
   - Detects WSL interop issues and provides guidance
   - Automatically handles path conversions
   - Color-coded output
   - For .pfx files: `./sign-packages.sh /path/to/cert.pfx [password]`
   - For cert store: redirects to PowerShell script if needed

## Certificate Requirements

### Current Certificate
- **Provider**: Certum Code Signing Certificate
- **Thumbprint**: D4CF73C16E699353F1D2222237D2250448850D2B
- **Timestamper**: http://timestamp.digicert.com

### Supported Formats
- **.pfx** - Personal Information Exchange (certificate + private key)
- **Windows Certificate Store** - Installed certificates (requires nuget.exe)

## Signing Process

### Using Certificate Store (Recommended)

The `sign-packages-certstore.ps1` script will:

1. Locate certificate in Windows Certificate Store (CurrentUser\My)
2. Prompt for confirmation before signing
3. Sign each package:
   - Orleans.StateMachineES.1.0.6.nupkg
   - Orleans.StateMachineES.Abstractions.1.0.6.nupkg
   - Orleans.StateMachineES.Generators.1.0.6.nupkg
4. Add timestamp from DigiCert (SHA256)
5. Verify each signature
6. Display summary

**Requirements:**
- Certificate installed in Windows Certificate Store (CurrentUser\My)
- Smart card reader connected (if using smart card)
- dotnet SDK 8.0+ installed

### Using .pfx File

The scripts will:

1. Verify certificate file exists
2. Sign each package with certificate password
3. Add timestamp from DigiCert (SHA256)
4. Verify each signature
5. Display summary

## Manual Signing

If automated scripts fail, sign packages manually:

### Using Certificate Store

```powershell
# Using dotnet CLI with Windows Certificate Store
dotnet nuget sign packages\Orleans.StateMachineES.1.0.6.nupkg `
  --certificate-fingerprint D4CF73C16E699353F1D2222237D2250448850D2B `
  --certificate-store-name My `
  --certificate-store-location CurrentUser `
  --timestamper http://timestamp.digicert.com `
  --overwrite

# Repeat for other packages
```

### Using .pfx File

```bash
# Using dotnet CLI with certificate file
dotnet nuget sign packages/Orleans.StateMachineES.1.0.6.nupkg \
  --certificate-path /path/to/cert.pfx \
  --certificate-password "password" \
  --timestamper http://timestamp.digicert.com \
  --overwrite

# Repeat for other packages
```

## Verification

After signing, verify signatures:

```bash
# Verify all packages
dotnet.exe nuget verify packages/*.nupkg --all

# Verify specific package
dotnet.exe nuget verify packages/Orleans.StateMachineES.1.0.6.nupkg --all
```

## Troubleshooting

### Error: "WSL Windows interop is not working"
- **Solution**: Run `sign-packages-certstore.ps1` from Windows PowerShell instead
- WSL interop can be broken on some systems
- From Windows: `powershell -ExecutionPolicy Bypass -File .\sign-packages-certstore.ps1`

### Error: "Certificate not found in store"
- **Check certificate installation**: Run `certmgr.msc` in Windows
- Certificate should be in: `Personal > Certificates`
- Verify thumbprint matches: `D4CF73C16E699353F1D2222237D2250448850D2B`
- For smart cards: ensure card reader is connected and card is inserted

### Error: "Smart card reader not responding"
- Reconnect the card reader
- Try removing and reinserting the smart card
- Check Windows Device Manager for reader status
- Some readers require specific drivers

### Error: "dotnet.exe not found"
- Install .NET SDK on Windows
- Ensure `/mnt/c/Program Files/dotnet/dotnet.exe` exists
- Run script from WSL, not native Linux

### Error: "Certificate file not found" (for .pfx files)
- Verify certificate path is correct
- Use absolute path: `/home/user/certs/cert.pfx`
- Ensure file has .pfx extension

### Error: "Invalid certificate password" (for .pfx files)
- Verify password is correct
- Try without password if certificate is unprotected
- Check for special characters requiring escaping

### Error: "Timestamping failed"
- Check internet connection
- Timestamp server may be temporarily unavailable
- Try again after a few minutes
- Alternative timestampers:
  - `http://timestamp.digicert.com` (current)
  - `http://time.certum.pl`
  - `http://timestamp.sectigo.com`

## Next Steps After Signing

1. **Verify Signatures**
   ```bash
   dotnet nuget verify packages/*.nupkg --all
   ```

2. **Test Installation**
   ```bash
   dotnet nuget push packages/*.nupkg --source local-test
   dotnet add package Orleans.StateMachineES --version 1.0.6 --source local-test
   ```

3. **Publish to NuGet.org**
   ```bash
   dotnet nuget push packages/Orleans.StateMachineES.1.0.6.nupkg \
     --api-key YOUR_API_KEY \
     --source https://api.nuget.org/v3/index.json

   dotnet nuget push packages/Orleans.StateMachineES.Abstractions.1.0.6.nupkg \
     --api-key YOUR_API_KEY \
     --source https://api.nuget.org/v3/index.json

   dotnet nuget push packages/Orleans.StateMachineES.Generators.1.0.6.nupkg \
     --api-key YOUR_API_KEY \
     --source https://api.nuget.org/v3/index.json
   ```

## Security Notes

- Never commit certificate files (.pfx) to source control
- Never commit certificate passwords
- Add to .gitignore: `*.pfx`, `*.p12`, `certificates/`
- Store certificates in secure location
- Use environment variables for passwords in CI/CD

## References

- [NuGet Package Signing](https://learn.microsoft.com/en-us/nuget/create-packages/sign-a-package)
- [dotnet nuget sign](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-sign)
- [Code Signing Best Practices](https://learn.microsoft.com/en-us/windows/win32/seccrypto/cryptography-tools)
