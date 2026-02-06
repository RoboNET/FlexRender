#!/bin/bash

# Visual Documentation Structure Verification
# Checks that all files follow naming conventions and are properly referenced

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
WIKI_DIR="$PROJECT_ROOT/docs/wiki"
OUTPUT_DIR="$SCRIPT_DIR/output"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo "========================================="
echo "Visual Documentation Structure Verification"
echo "========================================="
echo ""

errors=0
warnings=0

# Check 1: YAML files without category prefix in output
echo "1. Checking PNG naming convention..."
unprefixed=$(find "$OUTPUT_DIR" -name "*.png" -type f | while read png; do
    basename=$(basename "$png")
    # Check if filename has a category prefix (e.g., "align-", "canvas-")
    # Allow lowercase letters, hyphens, and camelCase in the feature name
    if [[ ! "$basename" =~ ^[a-z]+-[a-zA-Z-]+\.png$ ]]; then
        echo "  - $basename"
    fi
done)

if [ -z "$unprefixed" ]; then
    echo -e "  ${GREEN}✓${NC} All PNG files follow naming convention"
else
    echo -e "  ${RED}✗${NC} Files without category prefix:"
    echo "$unprefixed"
    errors=$((errors + 1))
fi
echo ""

# Check 2: Orphaned PNG files (no corresponding YAML)
echo "2. Checking for orphaned PNG files..."
orphaned=$(find "$OUTPUT_DIR" -name "*.png" -type f | while read png; do
    basename=$(basename "$png" .png)

    # Extract category and feature from PNG name
    if [[ "$basename" =~ ^([a-z]+)-(.+)$ ]]; then
        category="${BASH_REMATCH[1]}"
        feature="${BASH_REMATCH[2]}"
        yaml_file="$SCRIPT_DIR/$category/$feature.yaml"

        if [ ! -f "$yaml_file" ]; then
            echo "  - $basename.png (no $category/$feature.yaml)"
        fi
    fi
done)

if [ -z "$orphaned" ]; then
    echo -e "  ${GREEN}✓${NC} No orphaned PNG files"
else
    echo -e "  ${YELLOW}⚠${NC} Orphaned PNG files:"
    echo "$orphaned"
    warnings=$((warnings + 1))
fi
echo ""

# Check 3: Missing PNG files for YAML templates
echo "3. Checking for missing PNG files..."
missing=$(find "$SCRIPT_DIR" -name "*.yaml" -type f | while read yaml; do
    category=$(basename "$(dirname "$yaml")")
    filename=$(basename "$yaml" .yaml)
    png_file="$OUTPUT_DIR/${category}-${filename}.png"

    if [ ! -f "$png_file" ]; then
        echo "  - ${category}-${filename}.png (from $category/$filename.yaml)"
    fi
done)

if [ -z "$missing" ]; then
    echo -e "  ${GREEN}✓${NC} All YAML files have corresponding PNG"
else
    echo -e "  ${RED}✗${NC} Missing PNG files:"
    echo "$missing"
    errors=$((errors + 1))
fi
echo ""

# Check 4: Wiki references to non-existent PNG files
echo "4. Checking wiki references..."
broken_count=0
if [ -d "$WIKI_DIR" ]; then
    while IFS= read -r match; do
        file=$(echo "$match" | cut -d: -f1)
        line=$(echo "$match" | cut -d: -f2-)

        # Extract PNG filename from the line
        if [[ "$line" =~ visual-docs/output/([^\)]+\.png) ]]; then
            png_name="${BASH_REMATCH[1]}"
            png_path="$SCRIPT_DIR/output/$png_name"

            if [ ! -f "$png_path" ]; then
                if [ $broken_count -eq 0 ]; then
                    echo -e "  ${RED}✗${NC} Broken wiki links:"
                fi
                echo "  - $png_name (in $(basename "$file"))"
                broken_count=$((broken_count + 1))
            fi
        fi
    done < <(grep -r "visual-docs/output/.*\.png" "$WIKI_DIR"/Visual-*.md 2>/dev/null || true)
fi

if [ $broken_count -eq 0 ]; then
    echo -e "  ${GREEN}✓${NC} All wiki links point to existing files"
else
    errors=$((errors + broken_count))
fi
echo ""

# Check 5: Duplicate PNG files in subdirectories
echo "5. Checking for PNG files in subdirectories..."
subdir_pngs=$(find "$SCRIPT_DIR" -name "*.png" -type f ! -path "*/output/*" ! -name "test-pattern.png" | while read png; do
    echo "  - $png"
done)

if [ -z "$subdir_pngs" ]; then
    echo -e "  ${GREEN}✓${NC} No PNG files in subdirectories (except test-pattern.png)"
else
    echo -e "  ${YELLOW}⚠${NC} PNG files should be in output/ only:"
    echo "$subdir_pngs"
    warnings=$((warnings + 1))
fi
echo ""

# Statistics
echo "========================================="
echo "Statistics"
echo "========================================="
yaml_count=$(find "$SCRIPT_DIR" -name "*.yaml" -type f | wc -l | tr -d ' ')
png_count=$(find "$OUTPUT_DIR" -name "*.png" -type f | wc -l | tr -d ' ')
wiki_count=$(find "$WIKI_DIR" -name "Visual-*.md" | wc -l | tr -d ' ')

echo "YAML templates: $yaml_count"
echo "PNG outputs:    $png_count"
echo "Wiki pages:     $wiki_count"
echo ""

# Summary
echo "========================================="
echo "Summary"
echo "========================================="

if [ $errors -eq 0 ] && [ $warnings -eq 0 ]; then
    echo -e "${GREEN}✓ All checks passed!${NC}"
    exit 0
elif [ $errors -eq 0 ]; then
    echo -e "${YELLOW}⚠ $warnings warning(s)${NC}"
    exit 0
else
    echo -e "${RED}✗ $errors error(s), $warnings warning(s)${NC}"
    exit 1
fi
