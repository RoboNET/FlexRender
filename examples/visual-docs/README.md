# Visual Documentation Guide

This directory contains visual examples for the FlexRender wiki documentation. Each YAML template demonstrates a specific feature or property, with corresponding PNG output images.

## Directory Structure

```
visual-docs/
├── README.md           # This file
├── output/             # All generated PNG files (63 files)
│   ├── align-*.png     # Alignment examples (5)
│   ├── barcode-*.png   # Barcode examples (2)
│   ├── border-*.png    # Border examples (4)
│   ├── canvas-*.png    # Canvas property examples (9)
│   ├── direction-*.png # Direction examples (9)
│   ├── image-*.png     # Image fit mode examples (4)
│   ├── justify-*.png   # Justify content examples (6)
│   ├── order-*.png     # Order examples (2)
│   ├── qr-*.png        # QR code examples (2)
│   ├── separator-*.png # Separator examples (3)
│   ├── text-*.png      # Text examples (5)
│   └── wrap-*.png      # Wrap examples (3)
├── align/              # Cross-axis alignment (5 YAML)
├── barcode/            # Barcode demonstrations (2 YAML)
├── border/             # Border styling (4 YAML)
├── canvas/             # Canvas settings (9 YAML)
├── direction/          # Flex direction and RTL (9 YAML)
├── image/              # Image fit modes (4 YAML + test-pattern.png symlink)
├── justify/            # Main-axis alignment (6 YAML)
├── order/              # Flex item ordering (2 YAML)
├── qr/                 # QR code demonstrations (2 YAML)
├── separator/          # Separator styling (3 YAML)
├── text/               # Text formatting (5 YAML)
└── wrap/               # Flex wrapping (3 YAML)
```

## File Naming Convention

All PNG files follow a strict naming pattern:

```
{category}-{feature}.png
```

Examples:
- `align-center.png` - Cross-axis center alignment
- `canvas-rotate-right.png` - Canvas rotation (right/90°)
- `image-contain.png` - Image fit mode: contain
- `justify-space-between.png` - Justify content: space-between

**Important:** Never create PNG files without category prefixes. This maintains consistency and prevents naming conflicts.

## When to Update

Update visual documentation when:

1. **Adding New Properties**
   - Create YAML template in appropriate category directory
   - Run regeneration script to create PNG
   - Add reference to corresponding wiki page

2. **Changing Behavior**
   - If layout engine changes affect visual output
   - If element rendering changes
   - After bug fixes that alter visual appearance

3. **Adding New Element Types**
   - Create new category directory if needed
   - Follow naming convention for files
   - Update this README with new category count

## How to Regenerate Images

### Regenerate All Images

From the project root:

```bash
cd examples/visual-docs

# Regenerate all visual examples
for yaml in */*.yaml; do
  category=$(dirname "$yaml")
  basename=$(basename "$yaml" .yaml)
  output="output/${category}-${basename}.png"

  echo "Rendering: $yaml → $output"
  dotnet run --project ../../src/FlexRender.Cli --no-build --framework net8.0 -- render "$yaml" -o "$output"
done
```

### Regenerate Single Category

```bash
# Example: regenerate all align examples
cd examples/visual-docs
for yaml in align/*.yaml; do
  basename=$(basename "$yaml" .yaml)
  output="output/align-${basename}.png"
  dotnet run --project ../../src/FlexRender.Cli --no-build --framework net8.0 -- render "$yaml" -o "$output"
done
```

### Regenerate Single File

```bash
cd examples/visual-docs
dotnet run --project ../../src/FlexRender.Cli --no-build --framework net8.0 -- \
  render align/center.yaml -o output/align-center.png
```

## Adding a New Example

1. **Create YAML Template**

   Choose the appropriate category directory (or create a new one):

   ```bash
   # Example: add new justify example
   cd examples/visual-docs/justify
   # Create your-feature.yaml
   ```

2. **Follow Template Structure**

   ```yaml
   template:
     name: "category-feature"
     version: 1

   fonts:
     default: "../../assets/fonts/Inter-Regular.ttf"

   canvas:
     fixed: width
     width: 400
     background: "#ffffff"

   layout:
     # Your example layout
   ```

3. **Generate PNG**

   ```bash
   dotnet run --project ../../src/FlexRender.Cli --framework net8.0 -- \
     render justify/your-feature.yaml -o output/justify-your-feature.png
   ```

4. **Update Wiki Documentation**

   Add reference in `docs/wiki/Visual-Reference.md` (single consolidated page):

   ```markdown
   | `your-feature` | Description | ![your-feature](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/category-your-feature.png) |
   ```

5. **Verify**

   - Check PNG was created in `output/`
   - Verify file follows naming convention
   - Test wiki link renders correctly

## Quality Guidelines

### YAML Templates

- Use consistent canvas width (typically 400-600px)
- Set explicit `background: "#ffffff"` for clarity
- Keep examples simple and focused on one feature
- Add descriptive text labels when helpful
- Use consistent color palette across examples

### PNG Outputs

- Should be clear and readable at actual size
- Avoid unnecessary whitespace
- Text should be legible (minimum 12px font)
- Use contrasting colors for visibility

## Maintenance Checklist

After making changes to visual docs:

- [ ] All YAML files render without errors
- [ ] All PNG files follow naming convention
- [ ] No duplicate or orphaned PNG files
- [ ] Wiki links point to correct PNG files
- [ ] README counts are accurate
- [ ] Git status shows only intended changes

## Troubleshooting

**Problem:** PNG file is missing or outdated

**Solution:** Regenerate specific file or entire category

---

**Problem:** Wiki page shows broken image

**Solution:**
1. Check PNG exists: `ls output/{category}-{feature}.png`
2. Verify wiki link path: `../../examples/visual-docs/output/{category}-{feature}.png`
3. Ensure category prefix matches directory name

---

**Problem:** Naming conflicts or duplicates

**Solution:**
1. Use `git status` to identify duplicates
2. Remove files without category prefixes
3. Follow strict naming convention

---

## Current Statistics

- **Total YAML templates:** 63
- **Total PNG outputs:** 63 (1:1 mapping)
- **Categories:** 13 (align, barcode, border, canvas, direction, image, justify, order, position, qr, separator, text, wrap)
- **Wiki pages:** 1 (Visual-Reference.md — consolidated)

Last updated: February 2026

## Source Assets

Some templates require source images for rendering (e.g., image fit mode examples). These assets are stored centrally and linked:

- **Location:** `examples/assets/placeholder/`
- **Usage:** Symbolic links from category directories (e.g., `image/test-pattern.png` → `../../assets/placeholder/test-pattern.png`)
- **Purpose:** Avoid duplication while keeping templates self-contained

**Available assets:**
- `test-pattern.png` - 300×200px test image with colored sections (used by image fit examples)
- `star-badge.png` - 24×24px golden star icon for premium/large orders (used in receipt-dynamic.yaml)
- `sample-a.png` - Generic placeholder image
- `sample-b.png` - Generic placeholder image

When adding new examples that require source images, place them in `examples/assets/placeholder/` and create symbolic links in the category directory.
