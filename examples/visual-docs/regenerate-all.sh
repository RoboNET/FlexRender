#!/bin/bash

# Visual Documentation Regeneration Script
# Regenerates all PNG files from YAML templates

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/output"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo "========================================="
echo "Visual Documentation Regeneration"
echo "========================================="
echo ""

# Check if CLI is built
if [ ! -f "$PROJECT_ROOT/src/FlexRender.Cli/bin/Debug/net8.0/FlexRender.Cli.dll" ]; then
    echo -e "${YELLOW}CLI not found. Building...${NC}"
    dotnet build "$PROJECT_ROOT/src/FlexRender.Cli" --configuration Debug
    echo ""
fi

# Count YAML files
yaml_count=$(find "$SCRIPT_DIR" -name "*.yaml" -type f | wc -l | tr -d ' ')
echo -e "${GREEN}Found $yaml_count YAML templates${NC}"
echo ""

# Create output directory if it doesn't exist
mkdir -p "$OUTPUT_DIR"

# Counter for progress
current=0
success=0
failed=0

# Process each YAML file
for yaml in "$SCRIPT_DIR"/*/*.yaml; do
    if [ ! -f "$yaml" ]; then
        continue
    fi

    current=$((current + 1))

    # Extract category and basename
    category=$(basename "$(dirname "$yaml")")
    filename=$(basename "$yaml" .yaml)
    output_file="$OUTPUT_DIR/${category}-${filename}.png"

    # Show progress
    echo -e "${YELLOW}[$current/$yaml_count]${NC} Rendering: ${category}/${filename}.yaml"

    # Render the template
    if dotnet run --project "$PROJECT_ROOT/src/FlexRender.Cli" --no-build --framework net8.0 -- \
        render "$yaml" -o "$output_file" 2>&1 | grep -q "Rendered"; then
        echo -e "  ${GREEN}✓${NC} Success: $output_file"
        success=$((success + 1))
    else
        echo -e "  ${RED}✗${NC} Failed: $yaml"
        failed=$((failed + 1))
    fi
    echo ""
done

# Summary
echo "========================================="
echo "Summary"
echo "========================================="
echo -e "${GREEN}Success: $success${NC}"
if [ $failed -gt 0 ]; then
    echo -e "${RED}Failed:  $failed${NC}"
fi
echo ""

# Count PNG files in output
png_count=$(find "$OUTPUT_DIR" -name "*.png" -type f | wc -l | tr -d ' ')
echo "PNG files in output/: $png_count"

if [ $png_count -eq $yaml_count ]; then
    echo -e "${GREEN}✓ All templates rendered successfully (1:1 mapping)${NC}"
else
    echo -e "${YELLOW}⚠ PNG count ($png_count) doesn't match YAML count ($yaml_count)${NC}"
    echo "  This may indicate missing or extra PNG files."
fi

echo ""
echo "Done!"

exit 0
