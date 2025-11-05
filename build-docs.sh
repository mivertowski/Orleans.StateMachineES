#!/bin/bash

# Build Orleans.StateMachineES Documentation
# This script builds the projects and generates DocFx documentation

set -e  # Exit on error

echo "==================================="
echo "Building Orleans.StateMachineES Documentation"
echo "==================================="
echo ""

# Step 1: Restore dependencies
echo "[1/4] Restoring dependencies..."
dotnet restore

# Step 2: Build projects (generates XML documentation)
echo "[2/4] Building projects..."
dotnet build --no-restore --configuration Release

# Step 3: Install DocFx if not present
echo "[3/4] Checking DocFx installation..."
if ! command -v docfx &> /dev/null; then
    echo "DocFx not found. Installing..."
    dotnet tool install -g docfx
else
    echo "DocFx already installed."
fi

# Step 4: Build documentation
echo "[4/4] Building documentation..."
cd docfx
docfx docfx.json

echo ""
echo "==================================="
echo "Documentation build complete!"
echo "==================================="
echo ""
echo "Output directory: docfx/_site/"
echo ""
echo "To serve documentation locally, run:"
echo "  cd docfx"
echo "  docfx serve _site"
echo ""
echo "Then open: http://localhost:8080"
echo ""
