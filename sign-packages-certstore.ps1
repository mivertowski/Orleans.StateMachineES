# NuGet Package Signing Script for Orleans.StateMachineES v1.0.6
# Signs packages using certificate from Windows Certificate Store
#
# Certificate Details:
# - Subject: CN=Michael Ivertowski, O=Michael Ivertowski, L=Uster, S=Zurich, C=CH
# - Thumbprint (SHA-256): D4CF73C16E699353F1D2222237D2250448850D2B
# - Valid until: 23.09.2026
#
# Requirements:
# - Certificate installed in Windows Certificate Store (CurrentUser\My)
# - Smart card reader connected with certificate card inserted
# - dotnet SDK 8.0+ installed

param(
    [string]$PackagesPath = ".\packages",
    [string]$CertificateFingerprint = "D4CF73C16E699353F1D2222237D2250448850D2B",
    [string]$CertificateStoreName = "My",
    [string]$CertificateStoreLocation = "CurrentUser",
    [string]$Timestamper = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " Orleans.StateMachineES v1.0.6 Package Signing" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Check if packages directory exists
if (!(Test-Path $PackagesPath)) {
    Write-Host "ERROR: Packages directory not found: $PackagesPath" -ForegroundColor Red
    exit 1
}

# Get all packages to sign
$packages = Get-ChildItem -Path $PackagesPath -Filter "*.nupkg" | Where-Object { $_.Name -notlike "*.snupkg" }

if ($packages.Count -eq 0) {
    Write-Host "ERROR: No packages found in: $PackagesPath" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($packages.Count) package(s) to sign" -ForegroundColor Green
Write-Host ""
Write-Host "Certificate Details:" -ForegroundColor Cyan
Write-Host "  SHA-256 Fingerprint: $CertificateFingerprint"
Write-Host "  Store: $CertificateStoreLocation\$CertificateStoreName"
Write-Host "  Timestamper: $Timestamper"
Write-Host ""

# Confirm before proceeding
$confirm = Read-Host "Press ENTER to start signing, or Ctrl+C to cancel"

Write-Host ""
Write-Host "Starting package signing..." -ForegroundColor Cyan
Write-Host ""

$signedCount = 0
$failedCount = 0

foreach ($package in $packages) {
    Write-Host "Signing: $($package.Name)" -ForegroundColor Cyan

    try {
        dotnet nuget sign $package.FullName `
            --certificate-fingerprint $CertificateFingerprint `
            --certificate-store-name $CertificateStoreName `
            --certificate-store-location $CertificateStoreLocation `
            --timestamper $Timestamper `
            --overwrite

        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Signed: $($package.Name)" -ForegroundColor Green

            # Verify signature
            Write-Host "Verifying signature..." -ForegroundColor Cyan
            dotnet nuget verify $package.FullName --all | Out-Null

            if ($LASTEXITCODE -eq 0) {
                Write-Host "[OK] Verified: $($package.Name)" -ForegroundColor Green
                $signedCount++
            } else {
                Write-Host "[WARN] Could not verify signature" -ForegroundColor Yellow
                Write-Host "Signed successfully" -ForegroundColor Green
                $signedCount++
            }
        } else {
            Write-Host "[FAIL] Signing failed: $($package.Name)" -ForegroundColor Red
            $failedCount++
        }
    }
    catch {
        Write-Host "[FAIL] Error signing $($package.Name): $_" -ForegroundColor Red
        $failedCount++
    }

    Write-Host ""
}

# Summary
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " Signing Summary" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Successfully signed: $signedCount" -ForegroundColor Green

if ($failedCount -gt 0) {
    Write-Host "Failed: $failedCount" -ForegroundColor Red
}

Write-Host ""

if ($failedCount -eq 0 -and $signedCount -gt 0) {
    Write-Host "SUCCESS: All packages signed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Verify signatures: dotnet nuget verify packages\*.nupkg --all"
    Write-Host "2. Push to NuGet.org: dotnet nuget push packages\*.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json"
    exit 0
} else {
    Write-Host "ERROR: Some packages failed to sign" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Cyan
    Write-Host "  - Make sure certificate is in Windows Certificate Store (CurrentUser\My)"
    Write-Host "  - Verify smart card reader is connected"
    Write-Host "  - Check certificate fingerprint matches: $CertificateFingerprint"
    Write-Host "  - Try running: certmgr.msc to view installed certificates"
    exit 1
}
