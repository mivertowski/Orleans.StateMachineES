# Package Signing Guide

This document describes how to sign Orleans.StateMachineES NuGet packages using Authenticode signing with a hardware token.

**Current Version:** 1.0.4

## Prerequisites

### Required Tools

1. **.NET SDK 9.0 or later**
   ```bash
   dotnet --version
   ```

2. **NuGet CLI** (for package signing)
   ```bash
   dotnet tool install --global NuGet.CommandLine
   ```

3. **SignTool.exe** (Windows SDK component)
   - Part of Windows SDK
   - Typically located at: `C:\Program Files (x86)\Windows Kits\10\bin\<version>\x64\signtool.exe`
   - Verify installation:
     ```bash
     signtool /?
     ```

4. **Hardware Token with Code Signing Certificate**
   - Must be connected to the system
   - Certificate should be valid for code signing
   - Token drivers must be installed

## Signing Workflow

### Step 1: Build Release Packages

```bash
# Clean previous builds
dotnet clean -c Release

# Build all projects in Release mode
dotnet build -c Release

# Create NuGet packages
dotnet pack -c Release -o ./packages
```

This will create three packages:
- `Orleans.StateMachineES.1.0.4.nupkg`
- `Orleans.StateMachineES.Abstractions.1.0.4.nupkg`
- `Orleans.StateMachineES.Generators.1.0.4.nupkg`

### Step 2: Identify Your Certificate

List available certificates on your hardware token:

```bash
# Windows (PowerShell)
Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert

# Alternative: Use certutil
certutil -store My
```

Note the **thumbprint** (SHA1 hash) of your code signing certificate.

### Step 3: Sign NuGet Packages

```bash
# Navigate to packages directory
cd ./packages

# Sign each package with your certificate thumbprint
nuget sign Orleans.StateMachineES.1.0.4.nupkg `
    -CertificateFingerprint <YOUR_THUMBPRINT> `
    -Timestamper http://timestamp.digicert.com `
    -TimestampHashAlgorithm SHA256

nuget sign Orleans.StateMachineES.Abstractions.1.0.4.nupkg `
    -CertificateFingerprint <YOUR_THUMBPRINT> `
    -Timestamper http://timestamp.digicert.com `
    -TimestampHashAlgorithm SHA256

nuget sign Orleans.StateMachineES.Generators.1.0.4.nupkg `
    -CertificateFingerprint <YOUR_THUMBPRINT> `
    -Timestamper http://timestamp.digicert.com `
    -TimestampHashAlgorithm SHA256
```

**Important Notes:**
- Replace `<YOUR_THUMBPRINT>` with your certificate's SHA1 thumbprint
- You may be prompted for your hardware token PIN
- Timestamping is crucial - it allows signatures to remain valid after certificate expiration

### Alternative: Batch Script

Create `sign-packages.ps1`:

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$Thumbprint
)

$packages = @(
    "Orleans.StateMachineES.1.0.4.nupkg",
    "Orleans.StateMachineES.Abstractions.1.0.4.nupkg",
    "Orleans.StateMachineES.Generators.1.0.4.nupkg"
)

foreach ($package in $packages) {
    Write-Host "Signing $package..."
    nuget sign $package `
        -CertificateFingerprint $Thumbprint `
        -Timestamper http://timestamp.digicert.com `
        -TimestampHashAlgorithm SHA256

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ $package signed successfully" -ForegroundColor Green
    } else {
        Write-Host "✗ Failed to sign $package" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`nAll packages signed successfully!" -ForegroundColor Green
```

Usage:
```bash
.\sign-packages.ps1 -Thumbprint <YOUR_THUMBPRINT>
```

### Step 4: Verify Signatures

Verify that packages are properly signed:

```bash
nuget verify -Signatures Orleans.StateMachineES.1.0.4.nupkg
nuget verify -Signatures Orleans.StateMachineES.Abstractions.1.0.4.nupkg
nuget verify -Signatures Orleans.StateMachineES.Generators.1.0.4.nupkg
```

Expected output:
```
Verifying Orleans.StateMachineES.1.0.4
Successfully verified package 'Orleans.StateMachineES.1.0.4'.
```

### Step 5: Push to NuGet.org

```bash
# Set your API key (once)
nuget setapikey <YOUR_NUGET_API_KEY>

# Push packages
nuget push Orleans.StateMachineES.1.0.4.nupkg -Source https://api.nuget.org/v3/index.json
nuget push Orleans.StateMachineES.Abstractions.1.0.4.nupkg -Source https://api.nuget.org/v3/index.json
nuget push Orleans.StateMachineES.Generators.1.0.4.nupkg -Source https://api.nuget.org/v3/index.json
```

## Timestamp Servers

If Digicert timestamp server is unavailable, try these alternatives:

- **Digicert**: http://timestamp.digicert.com
- **Sectigo**: http://timestamp.sectigo.com
- **GlobalSign**: http://timestamp.globalsign.com
- **SwissSign**: http://tsa.swisssign.net

## Troubleshooting

### Hardware Token Not Detected

1. Ensure token is properly connected
2. Verify drivers are installed
3. Restart the token or computer
4. Check Windows Device Manager for token status

### Certificate Not Found

```bash
# List all certificates
certutil -store My

# Check if certificate is for code signing
certutil -store My "<certificate_subject_name>"
```

### Signing Fails with "Access Denied"

- Run PowerShell as Administrator
- Check token PIN requirements
- Verify certificate has code signing usage

### Timestamp Server Timeout

- Check internet connectivity
- Try alternative timestamp server
- Increase timeout (if using custom scripts)

## Security Best Practices

1. **Never commit certificates or private keys** to version control
2. **Use environment variables** for sensitive data like thumbprints
3. **Secure your hardware token** - store it safely when not in use
4. **Use strong PINs** for hardware token protection
5. **Audit package signatures** before publishing
6. **Keep certificates up to date** - monitor expiration dates

## Automated CI/CD Signing

For GitHub Actions or Azure DevOps:

1. Store certificate thumbprint as secret
2. Use self-hosted runner with hardware token access
3. Automate signing in release pipeline

Example GitHub Actions step:

```yaml
- name: Sign NuGet Packages
  env:
    CERT_THUMBPRINT: ${{ secrets.CODE_SIGNING_THUMBPRINT }}
  run: |
    .\sign-packages.ps1 -Thumbprint $env:CERT_THUMBPRINT
```

## References

- [NuGet Package Signing](https://docs.microsoft.com/en-us/nuget/create-packages/sign-a-package)
- [SignTool Documentation](https://docs.microsoft.com/en-us/windows/win32/seccrypto/signtool)
- [Code Signing Best Practices](https://docs.microsoft.com/en-us/windows-hardware/drivers/install/code-signing-best-practices)

## Support

For signing issues:
- [GitHub Issues](https://github.com/mivertowski/Orleans.StateMachineES/issues)
- [NuGet Docs](https://docs.microsoft.com/en-us/nuget/)

---

**Last Updated**: January 2025
**Version**: 1.0.4
