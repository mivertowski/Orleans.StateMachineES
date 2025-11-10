#!/bin/bash
# NuGet Package Signing Script for Orleans.StateMachineES v1.0.6
# Supports both Windows Certificate Store and .pfx files

set -e  # Exit on error (except in signing loop where we handle errors)

# Configuration
PACKAGES_DIR="./packages"
TIMESTAMP_SERVER="http://timestamp.digicert.com"
# SHA-256 fingerprint required for dotnet nuget sign (64 hex characters)
# To get SHA-256 fingerprint: certutil -dump cert.cer | findstr "SHA256"
CERT_FINGERPRINT_SHA256="${CERT_FINGERPRINT_SHA256:-}"  # Set via environment variable
# Legacy SHA-1 thumbprint (40 hex characters) - for reference only
CERT_THUMBPRINT_SHA1="D4CF73C16E699353F1D2222237D2250448850D2B"
VERSION="1.0.6"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}=============================================${NC}"
echo -e "${CYAN}Orleans.StateMachineES v${VERSION} Package Signing${NC}"
echo -e "${CYAN}=============================================${NC}"
echo ""

# Check if running in WSL
if ! grep -qi microsoft /proc/version; then
    echo -e "${RED}ERROR: This script must be run from WSL${NC}"
    exit 1
fi

# Check if certificate path is provided as argument
CERT_PATH="$1"
CERT_PASSWORD="$2"

if [ -z "$CERT_PATH" ]; then
    echo -e "${CYAN}No certificate path provided. Using Windows Certificate Store...${NC}"
    echo ""
    USE_CERT_STORE=true

    # Auto-detect SHA-256 fingerprint if not provided
    if [ -z "$CERT_FINGERPRINT_SHA256" ]; then
        echo -e "${CYAN}Auto-detecting certificate SHA-256 fingerprint...${NC}"

        # Query Windows Certificate Store for SHA-256 fingerprint
        CERT_FINGERPRINT_SHA256=$(/init '/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe' -Command "\$cert = Get-ChildItem -Path Cert:\\CurrentUser\\My | Where-Object { \$_.Thumbprint -eq '$CERT_THUMBPRINT_SHA1' }; if (\$cert) { \$hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash(\$cert.RawData); [System.BitConverter]::ToString(\$hash).Replace('-','') }" 2>/dev/null | tr -d '\r\n')

        if [ -z "$CERT_FINGERPRINT_SHA256" ] || [ ${#CERT_FINGERPRINT_SHA256} -ne 64 ]; then
            echo -e "${RED}ERROR: Could not auto-detect SHA-256 fingerprint${NC}"
            echo -e "${YELLOW}Please provide it manually:${NC}"
            echo -e "  ${GREEN}CERT_FINGERPRINT_SHA256=<your-sha256-fingerprint> ./sign-packages.sh${NC}"
            echo ""
            echo -e "${CYAN}Or use a .pfx file:${NC}"
            echo -e "  ${GREEN}./sign-packages.sh /path/to/certificate.pfx [password]${NC}"
            exit 1
        fi

        echo -e "${GREEN}Detected SHA-256 Fingerprint: $CERT_FINGERPRINT_SHA256${NC}"
    else
        echo -e "${GREEN}Using provided SHA-256 Fingerprint: $CERT_FINGERPRINT_SHA256${NC}"
    fi

    echo -e "${GREEN}Store: CurrentUser\\My${NC}"
    echo -e "${GREEN}Timestamper: $TIMESTAMP_SERVER${NC}"
    echo ""

    # Windows temp directory for signing
    WIN_TEMP="/mnt/c/Temp"
    mkdir -p "$WIN_TEMP" 2>/dev/null

    echo -e "${CYAN}Using Windows dotnet.exe via /init (binfmt workaround)${NC}"
    echo -e "${GREEN}Signing from: C:\\Temp${NC}"
    echo ""
else
    # Using .pfx file with dotnet CLI
    if [ ! -f "$CERT_PATH" ]; then
        echo -e "${RED}ERROR: Certificate file not found: $CERT_PATH${NC}"
        exit 1
    fi

    USE_CERT_STORE=false

    # Use native WSL dotnet for .pfx files
    if ! command -v dotnet &> /dev/null; then
        echo -e "${RED}ERROR: dotnet not found${NC}"
        echo -e "${YELLOW}Please install .NET SDK in WSL${NC}"
        exit 1
    fi

    echo -e "${GREEN}Using: $(which dotnet) (version $(dotnet --version))${NC}"
    echo -e "${GREEN}Certificate file: $CERT_PATH${NC}"
    echo -e "${GREEN}Timestamp: $TIMESTAMP_SERVER${NC}"
    echo ""
fi

# Packages to sign
PACKAGES=(
    "Orleans.StateMachineES.${VERSION}.nupkg"
    "Orleans.StateMachineES.Abstractions.${VERSION}.nupkg"
    "Orleans.StateMachineES.Generators.${VERSION}.nupkg"
)

SUCCESS_COUNT=0
FAILED_COUNT=0

# Temporarily disable exit-on-error for signing loop
set +e

for package in "${PACKAGES[@]}"; do
    PACKAGE_PATH="$PACKAGES_DIR/$package"

    if [ ! -f "$PACKAGE_PATH" ]; then
        echo -e "${YELLOW}[SKIP] Package not found: $package${NC}"
        continue
    fi

    echo -e "${CYAN}Signing: $package${NC}"

    # Sign the package
    if [ "$USE_CERT_STORE" = true ]; then
        # Copy package to Windows temp directory
        cp "$PACKAGE_PATH" "$WIN_TEMP/"

        # Sign with certificate from Windows Certificate Store using Windows dotnet.exe via /init
        /init '/mnt/c/Windows/System32/cmd.exe' /c "cd C:\\Temp && C:\\Progra~1\\dotnet\\dotnet.exe nuget sign $package --certificate-fingerprint $CERT_FINGERPRINT_SHA256 --certificate-store-name My --certificate-store-location CurrentUser --timestamper $TIMESTAMP_SERVER --overwrite" > /dev/null 2>&1

        if [ $? -eq 0 ]; then
            # Copy signed package back
            cp "$WIN_TEMP/$package" "$PACKAGE_PATH"
            rm "$WIN_TEMP/$package"

            echo -e "${GREEN}[OK] Signed: $package${NC}"

            # Verify signature using Linux dotnet
            echo -e "${CYAN}Verifying signature...${NC}"
            dotnet nuget verify "$PACKAGE_PATH" --all > /dev/null 2>&1

            if [ $? -eq 0 ]; then
                echo -e "${GREEN}[OK] Verified: $package${NC}"
                ((SUCCESS_COUNT++))
            else
                echo -e "${YELLOW}[WARN] Could not verify signature${NC}"
                echo -e "${GREEN}Signed successfully${NC}"
                ((SUCCESS_COUNT++))
            fi
        else
            echo -e "${RED}[FAIL] Signing failed: $package${NC}"
            echo -e "${YELLOW}Make sure:${NC}"
            echo -e "${YELLOW}  - Certificate is in Windows Certificate Store (CurrentUser\\My)${NC}"
            echo -e "${YELLOW}  - Smart card reader is connected${NC}"
            echo -e "${YELLOW}  - Certificate SHA-256 fingerprint is correct${NC}"
            rm -f "$WIN_TEMP/$package"
            ((FAILED_COUNT++))
        fi
    else
        # Sign with certificate file using Linux dotnet CLI
        if [ -z "$CERT_PASSWORD" ]; then
            dotnet nuget sign "$PACKAGE_PATH" \
                --certificate-path "$CERT_PATH" \
                --timestamper "$TIMESTAMP_SERVER" \
                --overwrite
        else
            dotnet nuget sign "$PACKAGE_PATH" \
                --certificate-path "$CERT_PATH" \
                --certificate-password "$CERT_PASSWORD" \
                --timestamper "$TIMESTAMP_SERVER" \
                --overwrite
        fi

        if [ $? -eq 0 ]; then
            echo -e "${GREEN}[OK] Signed: $package${NC}"

            # Verify signature
            echo -e "${CYAN}Verifying signature...${NC}"
            dotnet nuget verify "$PACKAGE_PATH" --all > /dev/null 2>&1

            if [ $? -eq 0 ]; then
                echo -e "${GREEN}[OK] Verified: $package${NC}"
                ((SUCCESS_COUNT++))
            else
                echo -e "${RED}[FAIL] Verification failed: $package${NC}"
                ((FAILED_COUNT++))
            fi
        else
            echo -e "${RED}[FAIL] Signing failed: $package${NC}"
            ((FAILED_COUNT++))
        fi
    fi

    echo ""
done

# Re-enable exit-on-error
set -e

# Summary
echo -e "${CYAN}=============================================${NC}"
echo -e "${CYAN}Signing Summary${NC}"
echo -e "${CYAN}=============================================${NC}"

if [ $SUCCESS_COUNT -gt 0 ]; then
    echo -e "${GREEN}Successfully signed: $SUCCESS_COUNT packages${NC}"
fi

if [ $FAILED_COUNT -gt 0 ]; then
    echo -e "${RED}Failed: $FAILED_COUNT packages${NC}"
fi

echo ""

if [ $FAILED_COUNT -eq 0 ] && [ $SUCCESS_COUNT -gt 0 ]; then
    echo -e "${GREEN}SUCCESS: All packages signed!${NC}"
    echo ""
    echo -e "${CYAN}Next steps:${NC}"
    echo "1. Verify signatures: dotnet nuget verify packages/*.nupkg --all"
    echo "2. Push to NuGet.org: dotnet nuget push packages/*.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json"
    exit 0
else
    echo -e "${RED}ERROR: Some packages failed to sign${NC}"
    echo ""
    echo -e "${CYAN}Usage:${NC}"
    echo "  # Use Windows Certificate Store (with card reader):"
    echo "  ./sign-packages.sh"
    echo ""
    echo "  # Use .pfx file:"
    echo "  ./sign-packages.sh /path/to/certificate.pfx [password]"
    echo ""
    echo -e "${CYAN}Examples:${NC}"
    echo "  ./sign-packages.sh"
    echo "  ./sign-packages.sh ~/certificates/codesign.pfx mypassword"
    exit 1
fi
