#!/bin/bash

# =============================================================================
# Vid2Sub Test Script
# =============================================================================
# Usage:
#   ./scripts/test.sh          # build + unit tests
#   ./scripts/test.sh --full   # build + unit tests + publish checks
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TEST_PROJECT="$PROJECT_ROOT/tests/Vid2Sub.Tests/Vid2Sub.Tests.csproj"

FULL=false

for arg in "$@"; do
    case "$arg" in
        --full)
            FULL=true
            ;;
        -h|--help)
            echo "Usage: ./scripts/test.sh [--full]"
            echo ""
            echo "Options:"
            echo "  --full   Also run osx-arm64 and win-x64 publish checks"
            exit 0
            ;;
        *)
            echo "Error: Unknown option '$arg'"
            echo "Run './scripts/test.sh --help' for usage."
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"

echo "=========================================="
echo "Vid2Sub Tests"
echo "=========================================="
echo ""

echo "[1/2] Building..."
dotnet build

echo ""
echo "[2/2] Running unit tests..."
dotnet test "$TEST_PROJECT"

if [[ "$FULL" == true ]]; then
    echo ""
    echo "[full 1/2] Publishing osx-arm64..."
    dotnet publish -c Release -r osx-arm64 --self-contained

    echo ""
    echo "[full 2/2] Publishing win-x64..."
    dotnet publish -c Release -r win-x64 --self-contained
fi

echo ""
echo "=========================================="
echo "All checks passed."
echo "=========================================="
