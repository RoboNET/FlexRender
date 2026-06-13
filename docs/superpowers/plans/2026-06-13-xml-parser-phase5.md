# FlexRender.Xml Parser (Phase 5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new `FlexRender.Xml` package that parses an XML template syntax into the exact same `Template` AST the YAML parser produces, via the same `ITemplateParser` abstraction, with zero new elements and zero renderer changes.

**Architecture:** XML is translated into the same `YamlDotNet.RepresentationModel.YamlMappingNode` document tree that `FlexRender.Yaml.TemplateParser` already consumes internally, then handed to a new `internal` entry point on `TemplateParser` so that **all** existing element/chart/shape parsing, `KnownProperties` validation, typo suggestions, and `ResourceLimits` enforcement are reused verbatim. `FlexRender.Xml` references `FlexRender.Yaml`; `FlexRender.Yaml` grants `InternalsVisibleTo("FlexRender.Xml")`. Only the XML→node mapping, the public `XmlTemplateParser : ITemplateParser`, `RenderXml` extension methods, and docs are new.

**Tech Stack:** .NET (net8.0;net10.0), `System.Xml.Linq` (`XDocument`/`XElement` — AOT-safe, no reflection, NO `XmlSerializer`), YamlDotNet RepresentationModel (already a transitive dependency via `FlexRender.Yaml`), xUnit v3 + AwesomeAssertions for tests.

---

## XML Syntax Mapping (LOCKED DECISION)

The XML parser does **not** reimplement element parsing. It converts the XML tree into the equivalent `YamlMappingNode` structure and reuses the YAML element parsers. The mapping rules below define that conversion. They are designed to be LLM-friendly (attributes over indentation) while round-tripping cleanly to the existing AST.

### Document shape

| YAML | XML |
| --- | --- |
| top-level `template:`, `canvas:`, `fonts:`, `layout:` | a single root element `<flexrender>` |
| `canvas: { width: 300, ... }` | `<canvas width="300" .../>` child of `<flexrender>` |
| `template: { name, version, culture }` | attributes on `<flexrender>`: `name`, `version`, `culture` |
| `layout: [ ...elements ]` | every **other** child element of `<flexrender>` (in order) is a layout element |
| `fonts: { default: "x.ttf" }` | `<fonts>` child containing `<font name="default" path="x.ttf" fallback="Arial"/>` entries |

### Element shape

- **Element type = XML local element name.** `<text/>` → `type: text`, `<flex>` → `type: flex`, `<chart/>`, `<rect/>`, `<each>`, `<if>`, `<table>`, etc. The set of names is exactly the YAML element types.
- **Scalar properties = XML attributes.** `<text size="1em" color="#ff0000"/>` → `size: 1em`, `color: "#ff0000"`. Attribute names are identical to YAML property names (kebab-case and camelCase both pass through unchanged, e.g. `chart-type`, `stroke-width`, `min-width`, `borderColor`). Validation against `KnownProperties` is unchanged.
- **`text` content** may be given either as the `content` attribute **or** as the element's inner text: `<text>Hello</text>` ≡ `<text content="Hello"/>`. If both are present, the `content` attribute wins. (svg `content` follows the same rule.)
- **Child-element containers** map to YAML list properties. The container is expressed by repeating child elements directly inside the parent — no wrapper:
  - `flex` children → child layout elements directly inside `<flex>`.
  - `each` body → child layout elements directly inside `<each>` (YAML `children`).
  - `if` → `<then>` and `<else>` wrapper children, each holding layout elements; optional `<else-if>` wrapper holds a single nested `if` (YAML `elseIf`). Comparison operators (`equals`, `in`, `greaterThan`, …) remain attributes on `<if>`.
  - `table` → `<columns>` wrapper holding `<column .../>` entries; optional `<rows>` wrapper holding `<row .../>` entries. Column/row fields are attributes.
  - `chart` → `<series .../>` entries (each a child of `<chart>`), `<categories>` wrapper holding `<item>value</item>` entries, `<x-labels>`/`<y-labels>` wrappers holding `<item>` entries, `<palette>` wrapper holding `<color>#fff</color>` entries (or a `palette="ocean"` attribute for a named palette).
  - `draw` → `<shapes>` wrapper holding one-shape-per-child elements `<line/>`, `<polyline/>`, `<rect/>`, `<circle/>`, `<path/>`.
- **Scalar lists** (chart `series.data`, `categories`, polyline `points`):
  - `series` numeric data: `data="12,30,22,48"` attribute (comma-separated) → YAML `data: [12,30,22,48]`. An expression `data="{{ sales }}"` passes through as a scalar string.
  - tuple data (scatter/bubble): `points="1,2; 3,4; 5,6"` attribute on `<series>` → YAML `data: [[1,2],[3,4],[5,6]]`. (Semicolon separates points, comma separates components.)
  - `draw polyline` `points="10,10; 50,50"` → YAML `points: [[10,10],[50,50]]`.

### How the converter decides attribute vs. list-container

The converter is generic and data-driven. For each XML element it produces a `YamlMappingNode` with:
1. `type` = the element local-name (except the special wrappers below).
2. one scalar entry per attribute (name → value), with comma/semicolon list attributes expanded into `YamlSequenceNode` when the attribute name is a known list property (`data`, `points`, `categories`, `palette`, `x-labels`, `y-labels`).
3. one entry per recognised **child wrapper**: child elements named `then`/`else`/`else-if`/`columns`/`rows`/`series`/`categories`/`x-labels`/`y-labels`/`palette`/`shapes` become the corresponding YAML sequence/mapping; all **other** child elements are collected into the container's natural list key (`children` for `flex`/`each`, `layout` for the root, the shape list for `draw`, etc.).

### Side-by-side example (flex + chart)

YAML:

```yaml
canvas:
  width: 400
layout:
  - type: flex
    direction: row
    gap: 8
    children:
      - type: text
        content: "Quarterly sales"
        size: 1.2em
      - type: chart
        chart-type: bar
        categories: [Q1, Q2, Q3, Q4]
        series:
          - label: "2024"
            data: [12, 30, 22, 48]
          - label: "2025"
            data: [18, 26, 31, 40]
```

XML:

```xml
<flexrender>
  <canvas width="400"/>
  <flex direction="row" gap="8">
    <text size="1.2em">Quarterly sales</text>
    <chart chart-type="bar">
      <categories>
        <item>Q1</item><item>Q2</item><item>Q3</item><item>Q4</item>
      </categories>
      <series label="2024" data="12,30,22,48"/>
      <series label="2025" data="18,26,31,40"/>
    </chart>
  </flex>
</flexrender>
```

Both parse to the identical `Template` AST.

---

## File Structure

- `src/FlexRender.Xml/FlexRender.Xml.csproj` — new package; references `FlexRender.Yaml`.
- `src/FlexRender.Xml/XmlTemplateParser.cs` — public `ITemplateParser` for XML.
- `src/FlexRender.Xml/XmlToYamlNodeConverter.cs` — internal XElement → YamlMappingNode tree converter (all the mapping rules above).
- `src/FlexRender.Xml/XmlFlexRenderExtensions.cs` — `RenderXml` extension methods (mirror `RenderYaml`).
- `src/FlexRender.Yaml/Parsing/TemplateParser.cs` — add `internal Template ParseDocumentRoot(YamlMappingNode root)` + `InternalsVisibleTo`.
- `tests/FlexRender.Tests/Parsing/Xml/*.cs` — XML parser tests + YAML-vs-XML AST cross-checks.
- Docs: `docs/wiki/Xml-Syntax.md`, `AGENTS.md`, `llms.txt`, `llms-full.txt`.

---

### Task 1: Create FlexRender.Xml project and wire it into the solution

**Files:**
- Create: `src/FlexRender.Xml/FlexRender.Xml.csproj`
- Create: `src/FlexRender.Xml/Placeholder.cs`
- Modify: `FlexRender.slnx`

- [ ] **Step 1: Create the project file**

Create `src/FlexRender.Xml/FlexRender.Xml.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PackageId>FlexRender.Xml</PackageId>
    <Description>XML template parsing for FlexRender (alternative to YAML).</Description>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\FlexRender.Yaml\FlexRender.Yaml.csproj" />
  </ItemGroup>

</Project>
```

(No extra `PackageReference` is needed: `System.Xml.Linq` is in the framework reference, and YamlDotNet comes transitively through `FlexRender.Yaml`.)

- [ ] **Step 2: Create a temporary placeholder type so the project compiles**

Create `src/FlexRender.Xml/Placeholder.cs`:

```csharp
namespace FlexRender.Xml;

/// <summary>
/// Temporary placeholder so the empty project compiles. Removed in a later task.
/// </summary>
internal static class Placeholder
{
}
```

- [ ] **Step 3: Register the project in the solution**

In `FlexRender.slnx`, inside the `<Folder Name="/src/Parsers/">` element (which currently contains only `FlexRender.Yaml`), add a second project line directly after the existing one so the folder reads:

```xml
  <Folder Name="/src/Parsers/">
    <Project Path="src/FlexRender.Yaml/FlexRender.Yaml.csproj" />
    <Project Path="src/FlexRender.Xml/FlexRender.Xml.csproj" />
  </Folder>
```

- [ ] **Step 4: Build the solution**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 errors. `FlexRender.Xml` appears in the build output.

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Xml/FlexRender.Xml.csproj src/FlexRender.Xml/Placeholder.cs FlexRender.slnx
git commit --no-gpg-sign -m "build: scaffold FlexRender.Xml project"
```

---

### Task 2: Expose an internal AST entry point on TemplateParser

This lets `FlexRender.Xml` reuse all element/chart/shape parsing, `KnownProperties` validation, and `ResourceLimits` without duplication. `TemplateParser.Parse(string)` already builds a `YamlMappingNode root` and then parses metadata/fonts/canvas/layout from it (see lines 120-157). We extract that tail into an internal method.

**Files:**
- Modify: `src/FlexRender.Yaml/Parsing/TemplateParser.cs`
- Create: `src/FlexRender.Yaml/AssemblyInfo.cs`
- Test: `tests/FlexRender.Tests/Parsing/Xml/InternalEntryPointTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/InternalEntryPointTests.cs`:

```csharp
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for the internal <see cref="TemplateParser.ParseDocumentRoot"/> entry point
/// used by the XML parser to reuse YAML element parsing.
/// </summary>
public class InternalEntryPointTests
{
    /// <summary>
    /// Verifies that a programmatically built YamlMappingNode root produces the same AST
    /// as parsing the equivalent YAML string.
    /// </summary>
    [Fact]
    public void ParseDocumentRoot_BuiltNode_ProducesEquivalentAst()
    {
        // Build: { canvas: { width: 300 }, layout: [ { type: text, content: Hi } ] }
        var canvas = new YamlMappingNode();
        canvas.Add("width", "300");

        var textNode = new YamlMappingNode();
        textNode.Add("type", "text");
        textNode.Add("content", "Hi");

        var layout = new YamlSequenceNode();
        layout.Add(textNode);

        var root = new YamlMappingNode();
        root.Add("canvas", canvas);
        root.Add("layout", layout);

        var template = new TemplateParser().ParseDocumentRootForTests(root);

        var text = Assert.IsType<TextElement>(Assert.Single(template.Elements));
        Assert.Equal("Hi", text.Content);
        Assert.Equal(300, template.Canvas.Width);
    }
}
```

The test calls `ParseDocumentRootForTests` — an internal test shim we add next to the real internal method (the test project already has `InternalsVisibleTo` to `FlexRender.Tests`? It does not yet; we add it in Step 3 alongside the XML one).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~InternalEntryPointTests"`
Expected: FAIL — does not compile, `ParseDocumentRootForTests` is not defined.

- [ ] **Step 3: Add the internal entry point and InternalsVisibleTo**

In `src/FlexRender.Yaml/Parsing/TemplateParser.cs`, replace the body of `Parse(string content)` (the metadata/fonts/canvas/layout section, currently lines 120-157) so that after `root` is obtained it delegates to a new method. Specifically, change the `Parse(string)` method's tail (everything from `var template = new Template();` through `return template;`) to:

```csharp
        return ParseDocumentRoot(root);
    }

    /// <summary>
    /// Builds a <see cref="Template"/> from an already-parsed YAML document root.
    /// Shared by the YAML string/stream entry points and by the XML parser, which
    /// translates XML into the equivalent <see cref="YamlMappingNode"/> tree.
    /// </summary>
    /// <param name="root">The document root mapping node.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when required sections are missing or invalid.</exception>
    internal Template ParseDocumentRoot(YamlMappingNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var template = new Template();

        if (TryGetMapping(root, "template", out var templateNode))
        {
            template.Name = GetStringValue(templateNode, "name");
            template.Version = GetIntValue(templateNode, "version", 1);
            template.Culture = GetStringValue(templateNode, "culture");
        }

        var fontsKey = new YamlScalarNode("fonts");
        if (root.Children.TryGetValue(fontsKey, out var fontsYamlNode))
        {
            template.Fonts = fontsYamlNode switch
            {
                YamlMappingNode fontsMapping => ParseFonts(fontsMapping),
                YamlSequenceNode fontsSequence => ParseFontsList(fontsSequence),
                _ => throw new TemplateParseException(
                    "Invalid 'fonts' section. Expected a mapping (name: path) or a list of font entries.")
            };
        }

        if (!TryGetMapping(root, "canvas", out var canvasNode))
        {
            throw new TemplateParseException("Missing required 'canvas' section");
        }

        template.Canvas = ParseCanvas(canvasNode);

        if (TryGetSequence(root, "layout", out var layoutNode))
        {
            template.Elements = ParseElements(layoutNode);
        }

        return template;
    }

    /// <summary>
    /// Test-only shim exposing <see cref="ParseDocumentRoot"/> to the test assembly.
    /// </summary>
    /// <param name="root">The document root mapping node.</param>
    /// <returns>The parsed template.</returns>
    internal Template ParseDocumentRootForTests(YamlMappingNode root) => ParseDocumentRoot(root);
```

Make sure the original `Parse(string)` no longer contains the duplicated metadata/canvas/layout block (it now ends with `return ParseDocumentRoot(root);`).

Create `src/FlexRender.Yaml/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FlexRender.Xml")]
[assembly: InternalsVisibleTo("FlexRender.Tests")]
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~InternalEntryPointTests"`
Expected: PASS (1 test).

Then run the full YAML parser suite to confirm the refactor changed nothing:

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~Parsing.TemplateParser"`
Expected: PASS (all existing parser tests green).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Yaml/Parsing/TemplateParser.cs src/FlexRender.Yaml/AssemblyInfo.cs tests/FlexRender.Tests/Parsing/Xml/InternalEntryPointTests.cs
git commit --no-gpg-sign -m "refactor: extract ParseDocumentRoot entry point for parser reuse"
```

---

### Task 3: XmlTemplateParser skeleton + canvas + simple text/separator elements

Introduce the public parser and the converter, covering the document shape, `canvas`, and the two simplest elements. Delete the placeholder.

**Files:**
- Create: `src/FlexRender.Xml/XmlToYamlNodeConverter.cs`
- Create: `src/FlexRender.Xml/XmlTemplateParser.cs`
- Delete: `src/FlexRender.Xml/Placeholder.cs`
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlTemplateParserBasicTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlTemplateParserBasicTests.cs`:

```csharp
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for basic XML template parsing (canvas, text, separator).
/// </summary>
public class XmlTemplateParserBasicTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_CanvasAndText_ContentAttribute()
    {
        const string xml = """
            <flexrender>
              <canvas width="300" background="#ffffff"/>
              <text content="Hello World" size="1.5em" color="#ff0000"/>
            </flexrender>
            """;

        var template = _parser.Parse(xml);

        Assert.Equal(300, template.Canvas.Width);
        var text = Assert.IsType<TextElement>(Assert.Single(template.Elements));
        Assert.Equal("Hello World", text.Content);
        Assert.Equal("1.5em", text.Size.Value);
        Assert.Equal("#ff0000", text.Color.Value);
    }

    [Fact]
    public void Parse_TextInnerTextUsedAsContent()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <text size="1em">Inline body</text>
            </flexrender>
            """;

        var text = Assert.IsType<TextElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal("Inline body", text.Content);
    }

    [Fact]
    public void Parse_Separator()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <separator orientation="horizontal" style="dashed" thickness="2" color="#333333"/>
            </flexrender>
            """;

        var sep = Assert.IsType<SeparatorElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(SeparatorOrientation.Horizontal, sep.Orientation);
        Assert.Equal(SeparatorStyle.Dashed, sep.Style);
        Assert.Equal(2f, sep.Thickness);
    }

    [Fact]
    public void Parse_NullContent_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => _parser.Parse((string)null!));
    }

    [Fact]
    public void Parse_MissingCanvas_Throws()
    {
        const string xml = "<flexrender><text content=\"x\"/></flexrender>";
        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("canvas", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MalformedXml_ThrowsTemplateParseException()
    {
        const string xml = "<flexrender><canvas width=\"300\"></flexrender>";
        Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlTemplateParserBasicTests"`
Expected: FAIL — `XmlTemplateParser` does not exist.

- [ ] **Step 3: Implement the converter and parser**

Delete the placeholder: `git rm src/FlexRender.Xml/Placeholder.cs` (or remove the file).

Create `src/FlexRender.Xml/XmlToYamlNodeConverter.cs`:

```csharp
using System.Globalization;
using System.Xml.Linq;
using FlexRender.Parsing;
using YamlDotNet.RepresentationModel;

namespace FlexRender.Xml;

/// <summary>
/// Converts a FlexRender XML template tree into the equivalent
/// <see cref="YamlMappingNode"/> document root that the YAML
/// <see cref="TemplateParser"/> consumes, so all element/chart/shape parsing and
/// validation is reused without duplication.
/// </summary>
internal static class XmlToYamlNodeConverter
{
    /// <summary>The root element local-name.</summary>
    private const string RootName = "flexrender";

    /// <summary>
    /// Wrapper child element names that map to dedicated YAML list/branch keys rather than
    /// being treated as nested layout elements.
    /// </summary>
    private static readonly HashSet<string> WrapperNames = new(StringComparer.Ordinal)
    {
        "then", "else", "else-if", "columns", "rows", "series",
        "categories", "x-labels", "y-labels", "palette", "shapes"
    };

    /// <summary>
    /// Attribute names whose comma/semicolon values expand into YAML sequences.
    /// </summary>
    private static readonly HashSet<string> ListAttributes = new(StringComparer.Ordinal)
    {
        "data", "points", "categories", "palette", "x-labels", "y-labels"
    };

    /// <summary>
    /// Parses the XML string and builds the YAML document root mapping.
    /// </summary>
    /// <param name="xml">The raw XML template.</param>
    /// <returns>The equivalent document root mapping node.</returns>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or the root element is wrong.</exception>
    internal static YamlMappingNode Convert(string xml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new TemplateParseException($"Invalid XML: {ex.Message}", ex);
        }

        var rootEl = doc.Root
            ?? throw new TemplateParseException("XML template has no root element.");

        if (!string.Equals(rootEl.Name.LocalName, RootName, StringComparison.Ordinal))
        {
            throw new TemplateParseException(
                $"XML template root must be <{RootName}>, but was <{rootEl.Name.LocalName}>.");
        }

        var root = new YamlMappingNode();

        // template metadata from root attributes
        var metadata = new YamlMappingNode();
        AddAttrIfPresent(rootEl, "name", metadata);
        AddAttrIfPresent(rootEl, "version", metadata);
        AddAttrIfPresent(rootEl, "culture", metadata);
        if (metadata.Children.Count > 0)
        {
            root.Add("template", metadata);
        }

        var layout = new YamlSequenceNode();

        foreach (var child in rootEl.Elements())
        {
            var localName = child.Name.LocalName;
            switch (localName)
            {
                case "canvas":
                    root.Add("canvas", AttributesToMapping(child));
                    break;
                case "fonts":
                    root.Add("fonts", ConvertFonts(child));
                    break;
                default:
                    layout.Add(ConvertElement(child));
                    break;
            }
        }

        root.Add("layout", layout);
        return root;
    }

    /// <summary>
    /// Builds a mapping node containing only the element's attributes (no type, no children).
    /// </summary>
    private static YamlMappingNode AttributesToMapping(XElement el)
    {
        var node = new YamlMappingNode();
        foreach (var attr in el.Attributes())
        {
            node.Add(attr.Name.LocalName, new YamlScalarNode(attr.Value));
        }
        return node;
    }

    /// <summary>
    /// Converts a single layout element (recursively) into a YAML mapping node with a
    /// <c>type</c> entry, scalar attributes, and child-derived list properties.
    /// </summary>
    private static YamlMappingNode ConvertElement(XElement el)
    {
        var type = el.Name.LocalName;
        var node = new YamlMappingNode();
        node.Add("type", new YamlScalarNode(type));

        // Attributes -> scalar or list entries.
        foreach (var attr in el.Attributes())
        {
            var name = attr.Name.LocalName;
            if (ListAttributes.Contains(name))
            {
                node.Add(name, ExpandListAttribute(attr.Value));
            }
            else
            {
                node.Add(name, new YamlScalarNode(attr.Value));
            }
        }

        // Inner text -> content (only when no content attribute and there are no child elements).
        if (el.Attribute("content") is null && !el.HasElements)
        {
            var inner = el.Value;
            if (!string.IsNullOrWhiteSpace(inner))
            {
                node.Add("content", new YamlScalarNode(inner.Trim()));
            }
        }

        // Child elements.
        var naturalList = new YamlSequenceNode();
        foreach (var child in el.Elements())
        {
            var childName = child.Name.LocalName;
            if (WrapperNames.Contains(childName))
            {
                AddWrapper(node, type, child);
            }
            else
            {
                naturalList.Add(ConvertElement(child));
            }
        }

        if (naturalList.Children.Count > 0)
        {
            node.Add(NaturalListKey(type), naturalList);
        }

        return node;
    }

    /// <summary>
    /// Maps an element type to the YAML key its directly-nested layout children belong under.
    /// </summary>
    private static string NaturalListKey(string type) => type switch
    {
        "each" => "children",
        _ => "children" // flex and any other container use 'children'
    };

    /// <summary>
    /// Adds a recognised wrapper child (then/else/columns/series/...) to the node under its YAML key.
    /// </summary>
    private static void AddWrapper(YamlMappingNode node, string parentType, XElement wrapper)
    {
        var name = wrapper.Name.LocalName;
        switch (name)
        {
            case "then":
            case "else":
                node.Add(name, ConvertElementSequence(wrapper));
                break;
            case "else-if":
                var inner = wrapper.Elements().FirstOrDefault();
                if (inner is not null)
                {
                    node.Add("elseIf", ConvertElement(inner));
                }
                break;
            case "columns":
                node.Add("columns", ConvertAttributeItemSequence(wrapper));
                break;
            case "rows":
                node.Add("rows", ConvertAttributeItemSequence(wrapper));
                break;
            case "series":
                // Handled at element level for charts; placeholder (overridden in chart task).
                break;
            case "categories":
            case "x-labels":
            case "y-labels":
                node.Add(name, ConvertScalarItemSequence(wrapper));
                break;
            case "palette":
                node.Add("palette", ConvertScalarItemSequence(wrapper));
                break;
            case "shapes":
                // Handled in the draw task.
                break;
        }
    }

    /// <summary>Converts child layout elements of a wrapper into a YAML sequence of mappings.</summary>
    private static YamlSequenceNode ConvertElementSequence(XElement wrapper)
    {
        var seq = new YamlSequenceNode();
        foreach (var child in wrapper.Elements())
        {
            seq.Add(ConvertElement(child));
        }
        return seq;
    }

    /// <summary>Converts child elements whose attributes become mapping fields (table column/row).</summary>
    private static YamlSequenceNode ConvertAttributeItemSequence(XElement wrapper)
    {
        var seq = new YamlSequenceNode();
        foreach (var child in wrapper.Elements())
        {
            seq.Add(AttributesToMapping(child));
        }
        return seq;
    }

    /// <summary>Converts <c>&lt;item&gt;value&lt;/item&gt;</c> / <c>&lt;color&gt;</c> children into a scalar sequence.</summary>
    private static YamlSequenceNode ConvertScalarItemSequence(XElement wrapper)
    {
        var seq = new YamlSequenceNode();
        foreach (var child in wrapper.Elements())
        {
            seq.Add(new YamlScalarNode(child.Value.Trim()));
        }
        return seq;
    }

    /// <summary>Converts a &lt;fonts&gt; wrapper into a YAML sequence of font entry mappings.</summary>
    private static YamlSequenceNode ConvertFonts(XElement fonts)
    {
        var seq = new YamlSequenceNode();
        foreach (var font in fonts.Elements())
        {
            seq.Add(AttributesToMapping(font));
        }
        return seq;
    }

    /// <summary>
    /// Expands a comma-separated (or "x,y; x,y" tuple) attribute value into a YAML sequence.
    /// A value containing a template expression is left as a scalar.
    /// </summary>
    private static YamlNode ExpandListAttribute(string value)
    {
        if (value.Contains("{{", StringComparison.Ordinal))
        {
            return new YamlScalarNode(value);
        }

        // Tuple list: "1,2; 3,4" -> [[1,2],[3,4]]
        if (value.Contains(';', StringComparison.Ordinal))
        {
            var outer = new YamlSequenceNode();
            foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var inner = new YamlSequenceNode();
                foreach (var comp in part.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    inner.Add(new YamlScalarNode(comp));
                }
                outer.Add(inner);
            }
            return outer;
        }

        var seq = new YamlSequenceNode();
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            seq.Add(new YamlScalarNode(item));
        }
        return seq;
    }

    /// <summary>Adds an XML attribute to a mapping node when present and non-empty.</summary>
    private static void AddAttrIfPresent(XElement el, string name, YamlMappingNode node)
    {
        var attr = el.Attribute(name);
        if (attr is not null && !string.IsNullOrEmpty(attr.Value))
        {
            node.Add(name, new YamlScalarNode(attr.Value));
        }
    }

    /// <summary>Unused parameter guard for invariant-culture formatting helpers (kept for future numeric formatting).</summary>
    private static string FormatInvariant(double value) => value.ToString(CultureInfo.InvariantCulture);
}
```

> NOTE: `series` and `shapes` wrappers are intentionally left unhandled in `AddWrapper` here; Tasks 7 (shapes) and 8 (chart) add their handling. `FormatInvariant` is included to keep one invariant-culture helper available; if the analyzer flags it as unused (IDE0051/CA1823), inline-delete it in Task 8 when numeric formatting is actually used, or remove it now — see Step 4.

Create `src/FlexRender.Xml/XmlTemplateParser.cs`:

```csharp
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;

namespace FlexRender.Xml;

/// <summary>
/// Parses FlexRender XML templates into the same <see cref="Template"/> AST as the YAML parser.
/// XML is translated into the equivalent document tree and handed to the shared
/// <see cref="TemplateParser"/> so all element parsing, validation, and resource limits are reused.
/// </summary>
public sealed class XmlTemplateParser : ITemplateParser
{
    private readonly TemplateParser _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTemplateParser"/> class with default resource limits.
    /// </summary>
    public XmlTemplateParser() : this(new ResourceLimits())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTemplateParser"/> class with custom resource limits.
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public XmlTemplateParser(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _inner = new TemplateParser(limits);
    }

    /// <summary>
    /// Parses an XML template string into a <see cref="Template"/> AST.
    /// </summary>
    /// <param name="content">The XML template content.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or invalid.</exception>
    public Template Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new TemplateParseException("Template XML is empty or whitespace");
        }

        var root = XmlToYamlNodeConverter.Convert(content);
        return _inner.ParseDocumentRoot(root);
    }

    /// <summary>
    /// Parses an XML template from a stream.
    /// </summary>
    /// <param name="stream">The stream containing XML content.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or invalid.</exception>
    public Template Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
```

- [ ] **Step 4: Remove the unused FormatInvariant helper if the build flags it**

Run: `dotnet build FlexRender.slnx`
If the build fails on `FormatInvariant` (IDE0051 / CA1823, `TreatWarningsAsErrors`), delete the `FormatInvariant` method and its doc comment from `XmlToYamlNodeConverter.cs`. Re-run until: Build succeeded, 0 errors.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlTemplateParserBasicTests"`
Expected: PASS (6 tests).

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Xml/ tests/FlexRender.Tests/Parsing/Xml/XmlTemplateParserBasicTests.cs
git commit --no-gpg-sign -m "feat: XML template parser with canvas, text, separator"
```

---

### Task 4: Flex nesting and remaining leaf elements (qr/barcode/image/content)

The converter already recurses into non-wrapper children as `children`. Add tests proving flex nesting and the other leaf elements work through the shared parser.

**Files:**
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlFlexNestingTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlFlexNestingTests.cs`:

```csharp
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for nested flex containers and leaf elements via the XML parser.
/// </summary>
public class XmlFlexNestingTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_NestedFlexWithChildren()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <flex direction="row" gap="8" justify="center">
                <text content="Left"/>
                <flex direction="column">
                  <text content="A"/>
                  <text content="B"/>
                </flex>
              </flex>
            </flexrender>
            """;

        var flex = Assert.IsType<FlexElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(FlexDirection.Row, flex.Direction);
        Assert.Equal(JustifyContent.Center, flex.Justify);
        Assert.Equal(2, flex.Children.Count);

        var inner = Assert.IsType<FlexElement>(flex.Children[1]);
        Assert.Equal(FlexDirection.Column, inner.Direction);
        Assert.Equal(2, inner.Children.Count);
        Assert.Equal("A", Assert.IsType<TextElement>(inner.Children[0]).Content);
    }

    [Fact]
    public void Parse_QrBarcodeImage()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <qr data="hello" size="120" errorCorrection="H"/>
              <barcode data="12345" width="200" height="80" format="ean13"/>
              <image src="logo.png" width="100" height="50" fit="cover"/>
            </flexrender>
            """;

        var elements = _parser.Parse(xml).Elements;
        var qr = Assert.IsType<QrElement>(elements[0]);
        Assert.Equal("hello", qr.Data);
        Assert.Equal(ErrorCorrectionLevel.H, qr.ErrorCorrection);

        var barcode = Assert.IsType<BarcodeElement>(elements[1]);
        Assert.Equal(BarcodeFormat.Ean13, barcode.Format);

        var image = Assert.IsType<ImageElement>(elements[2]);
        Assert.Equal("logo.png", image.Src);
        Assert.Equal(ImageFit.Cover, image.Fit);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails or passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlFlexNestingTests"`
Expected: PASS — the converter already handles nested children and attribute mapping. If any assertion fails, fix `ConvertElement`/`NaturalListKey` until green. (This task is primarily a coverage lock for the generic recursion.)

- [ ] **Step 3: Commit**

```bash
git add tests/FlexRender.Tests/Parsing/Xml/XmlFlexNestingTests.cs
git commit --no-gpg-sign -m "test: XML flex nesting and qr/barcode/image elements"
```

---

### Task 5: Control flow — each / if (then/else/else-if)

**Files:**
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlControlFlowTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlControlFlowTests.cs`:

```csharp
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for each/if control-flow elements via the XML parser.
/// </summary>
public class XmlControlFlowTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_Each_WithChildren()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <each array="items" as="item">
                <text content="{{ item.name }}"/>
              </each>
            </flexrender>
            """;

        var each = Assert.IsType<EachElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal("items", each.ArrayPath);
        Assert.Equal("item", each.ItemVariable);
        Assert.Single(each.ItemTemplate);
        Assert.IsType<TextElement>(each.ItemTemplate[0]);
    }

    [Fact]
    public void Parse_If_ThenElse()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <if condition="paid" equals="true">
                <then>
                  <text content="PAID"/>
                </then>
                <else>
                  <text content="DUE"/>
                </else>
              </if>
            </flexrender>
            """;

        var ifEl = Assert.IsType<IfElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal("paid", ifEl.ConditionPath);
        Assert.Equal(ConditionOperator.Equals, ifEl.Operator);
        Assert.Equal("true", ifEl.CompareValue);
        Assert.Single(ifEl.ThenBranch);
        Assert.Single(ifEl.ElseBranch);
        Assert.Equal("PAID", Assert.IsType<TextElement>(ifEl.ThenBranch[0]).Content);
        Assert.Equal("DUE", Assert.IsType<TextElement>(ifEl.ElseBranch[0]).Content);
    }

    [Fact]
    public void Parse_If_ElseIf()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <if condition="status" equals="hot">
                <then><text content="HOT"/></then>
                <else-if>
                  <if condition="status" equals="warm">
                    <then><text content="WARM"/></then>
                  </if>
                </else-if>
              </if>
            </flexrender>
            """;

        var ifEl = Assert.IsType<IfElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.NotNull(ifEl.ElseIf);
        Assert.Equal("warm", ifEl.ElseIf!.CompareValue);
    }
}
```

> Verify property names against `IfElement`/`EachElement` (`ConditionPath`, `Operator`, `CompareValue`, `ThenBranch`, `ElseBranch`, `ElseIf`, `ItemTemplate`, `ArrayPath`, `ItemVariable`). If a name differs, adjust the test assertion to match the actual AST property.

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlControlFlowTests"`
Expected: PASS — `then`/`else`/`else-if` wrappers are already handled in `AddWrapper`, and `each` children fall through to `children`. If `else-if` fails, confirm `AddWrapper` adds key `elseIf` (the YAML parser reads `elseIf`).

- [ ] **Step 3: Commit**

```bash
git add tests/FlexRender.Tests/Parsing/Xml/XmlControlFlowTests.cs
git commit --no-gpg-sign -m "test: XML each/if control-flow parsing"
```

---

### Task 6: Table (columns / rows)

**Files:**
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlTableTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlTableTests.cs`:

```csharp
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for the table element via the XML parser.
/// </summary>
public class XmlTableTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_DynamicTable_Columns()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <table array="lines" as="line">
                <columns>
                  <column key="name" label="Item" grow="1"/>
                  <column key="price" label="Price" align="right"/>
                </columns>
              </table>
            </flexrender>
            """;

        var table = Assert.IsType<TableElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal("lines", table.ArrayPath);
        Assert.Equal(2, table.Columns.Count);
        Assert.Equal("name", table.Columns[0].Key);
        Assert.Equal("Item", table.Columns[0].Label);
        Assert.Equal(TextAlign.Right, table.Columns[1].Align);
    }

    [Fact]
    public void Parse_StaticTable_Rows()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <table>
                <columns>
                  <column key="name" label="Item"/>
                  <column key="qty" label="Qty"/>
                </columns>
                <rows>
                  <row name="Coffee" qty="2"/>
                  <row name="Tea" qty="1"/>
                </rows>
              </table>
            </flexrender>
            """;

        var table = Assert.IsType<TableElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Coffee", table.Rows[0].Values["name"]);
        Assert.Equal("1", table.Rows[1].Values["qty"]);
    }
}
```

> Confirm `TableElement` exposes `Columns`, `Rows`, `ArrayPath`, and `TableRow.Values` (a dictionary). Adjust assertions if the AST differs.

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlTableTests"`
Expected: PASS — `columns`/`rows` wrappers map to attribute-item sequences; the shared `ParseTableElement` builds the rest.

- [ ] **Step 3: Commit**

```bash
git add tests/FlexRender.Tests/Parsing/Xml/XmlTableTests.cs
git commit --no-gpg-sign -m "test: XML table columns/rows parsing"
```

---

### Task 7: Shapes — rect / circle / ellipse / draw (shapes wrapper)

The `draw` element's `shapes` wrapper holds one-shape-per-child elements. The YAML `draw` parser expects `shapes: [ { line: {...} }, { rect: {...} } ]` — each shape item is a mapping with a single shape-kind key whose value is the shape's attribute mapping. Implement that translation.

**Files:**
- Modify: `src/FlexRender.Xml/XmlToYamlNodeConverter.cs`
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlShapeTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlShapeTests.cs`:

```csharp
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for shape elements (rect, circle, ellipse, draw) via the XML parser.
/// </summary>
public class XmlShapeTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_RectCircleEllipse()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <rect fill="#4A90D9" stroke="#000000" stroke-width="2" radius="6" width="100" height="40"/>
              <circle fill="#ff0000" size="50"/>
              <ellipse fill="#00ff00" width="80" height="40"/>
            </flexrender>
            """;

        var elements = _parser.Parse(xml).Elements;
        var rect = Assert.IsType<RectElement>(elements[0]);
        Assert.Equal("#4A90D9", rect.Fill.Value);
        Assert.Equal(2f, rect.StrokeWidth.Value);

        Assert.IsType<CircleElement>(elements[1]);
        Assert.IsType<EllipseElement>(elements[2]);
    }

    [Fact]
    public void Parse_Draw_WithShapes()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <draw width="200" height="100">
                <shapes>
                  <line x1="0" y1="0" x2="100" y2="100" stroke="#000" stroke-width="2"/>
                  <rect x="10" y="10" width="50" height="30" fill="#eee"/>
                  <circle cx="80" cy="80" r="15" fill="#f00"/>
                  <polyline points="10,10; 50,50; 90,10" stroke="#00f" stroke-width="1"/>
                </shapes>
              </draw>
            </flexrender>
            """;

        var draw = Assert.IsType<DrawElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(4, draw.Shapes.Count);
        Assert.IsType<DrawLine>(draw.Shapes[0]);
        Assert.IsType<DrawRect>(draw.Shapes[1]);
        Assert.IsType<DrawCircle>(draw.Shapes[2]);
        Assert.IsType<DrawPolyline>(draw.Shapes[3]);
    }
}
```

> Confirm `DrawElement.Shapes` and the concrete shape types (`DrawLine`, `DrawRect`, `DrawCircle`, `DrawPolyline`, `DrawPath`) — they live in `src/FlexRender.Core/Parsing/Ast/DrawShapes.cs`.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlShapeTests"`
Expected: rect/circle/ellipse PASS; `Parse_Draw_WithShapes` FAILS — `shapes` wrapper is not yet translated (draw has 0 shapes).

- [ ] **Step 3: Implement the shapes wrapper translation**

In `src/FlexRender.Xml/XmlToYamlNodeConverter.cs`, in `AddWrapper`, replace the `case "shapes":` branch (currently a no-op comment) with:

```csharp
            case "shapes":
                node.Add("shapes", ConvertShapeSequence(wrapper));
                break;
```

Then add this method to the class:

```csharp
    /// <summary>
    /// Converts a &lt;shapes&gt; wrapper into the YAML <c>shapes</c> sequence, where each shape
    /// becomes a mapping with a single shape-kind key (line/polyline/rect/circle/path) whose value
    /// is the shape's attribute mapping (with any list attributes such as <c>points</c> expanded).
    /// </summary>
    private static YamlSequenceNode ConvertShapeSequence(XElement wrapper)
    {
        var seq = new YamlSequenceNode();
        foreach (var shape in wrapper.Elements())
        {
            var shapeMapping = new YamlMappingNode();
            foreach (var attr in shape.Attributes())
            {
                var name = attr.Name.LocalName;
                shapeMapping.Add(
                    name,
                    ListAttributes.Contains(name)
                        ? ExpandListAttribute(attr.Value)
                        : new YamlScalarNode(attr.Value));
            }

            var wrapped = new YamlMappingNode();
            wrapped.Add(shape.Name.LocalName, shapeMapping);
            seq.Add(wrapped);
        }
        return seq;
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlShapeTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Xml/XmlToYamlNodeConverter.cs tests/FlexRender.Tests/Parsing/Xml/XmlShapeTests.cs
git commit --no-gpg-sign -m "feat: XML draw shapes wrapper translation"
```

---

### Task 8: Chart — series / categories / palette / tuple data

The YAML chart parser expects `series: [ { label, data: [...] } ]`. The XML form uses repeated `<series .../>` children of `<chart>` (NOT wrapped). These are non-wrapper children, so the generic recursion would currently push them under `children`. Add explicit handling: `series` children collect into a YAML `series` sequence of attribute mappings (with `data`/`points` expanded), and they must NOT recurse as layout elements.

**Files:**
- Modify: `src/FlexRender.Xml/XmlToYamlNodeConverter.cs`
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlChartTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlChartTests.cs`:

```csharp
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for the chart element via the XML parser.
/// </summary>
public class XmlChartTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_BarChart_SeriesAndCategories()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <chart chart-type="bar" title="Sales" legend="bottom">
                <categories>
                  <item>Q1</item><item>Q2</item><item>Q3</item><item>Q4</item>
                </categories>
                <series label="2024" data="12,30,22,48"/>
                <series label="2025" data="18,26,31,40"/>
              </chart>
            </flexrender>
            """;

        var chart = Assert.IsType<ChartElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(ChartType.Bar, chart.ChartType);
        Assert.Equal("Sales", chart.Title);
        Assert.Equal(new[] { "Q1", "Q2", "Q3", "Q4" }, chart.Categories);
        Assert.Equal(2, chart.Series.Count);
        Assert.Equal("2024", chart.Series[0].Label);
        Assert.Equal(new[] { 12d, 30d, 22d, 48d }, chart.Series[0].Data);
    }

    [Fact]
    public void Parse_NamedPalette_Attribute()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <chart chart-type="pie" palette="ocean">
                <series data="10,20,30"/>
              </chart>
            </flexrender>
            """;

        var chart = Assert.IsType<ChartElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.NotNull(chart.Palette);
    }

    [Fact]
    public void Parse_ScatterTupleData()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <chart chart-type="scatter">
                <series label="pts" points="1,2; 3,4; 5,6"/>
              </chart>
            </flexrender>
            """;

        var chart = Assert.IsType<ChartElement>(Assert.Single(_parser.Parse(xml).Elements));
        var series = Assert.Single(chart.Series);
        Assert.Equal(3, series.Points.Count);
        Assert.Equal(3d, series.Points[1].X);
        Assert.Equal(4d, series.Points[1].Y);
    }
}
```

> The YAML series parser reads tuple data from the `data` key (an array-of-arrays). For scatter/bubble XML we map the `points` attribute on `<series>` to the YAML `data` key. Confirm `ChartPoint.X/Y` and `ChartSeries.Points`/`.Data` names against the AST. Also confirm `ChartElement.ChartType`, `.Categories`, `.Series`, `.Title`, `.Palette`.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlChartTests"`
Expected: FAIL — `<series>` children are not collected into the YAML `series` key (chart has 0 series / wrong shape).

- [ ] **Step 3: Implement chart series collection**

In `src/FlexRender.Xml/XmlToYamlNodeConverter.cs`:

First, prevent `<series>` from being treated as a generic layout child. In `ConvertElement`, change the child loop so series elements are collected separately. Replace the child-element loop body in `ConvertElement` (the `foreach (var child in el.Elements())` block) with:

```csharp
        // Child elements.
        var naturalList = new YamlSequenceNode();
        var seriesList = new YamlSequenceNode();
        foreach (var child in el.Elements())
        {
            var childName = child.Name.LocalName;
            if (string.Equals(childName, "series", StringComparison.Ordinal))
            {
                seriesList.Add(ConvertSeries(child));
            }
            else if (WrapperNames.Contains(childName))
            {
                AddWrapper(node, type, child);
            }
            else
            {
                naturalList.Add(ConvertElement(child));
            }
        }

        if (seriesList.Children.Count > 0)
        {
            node.Add("series", seriesList);
        }

        if (naturalList.Children.Count > 0)
        {
            node.Add(NaturalListKey(type), naturalList);
        }

        return node;
```

Then add the `ConvertSeries` method. It maps a `<series>` element's attributes to a series mapping, translating the XML `points` attribute (tuple data) into the YAML `data` array-of-arrays the chart parser expects, and expanding the flat `data` attribute into a numeric array:

```csharp
    /// <summary>
    /// Converts a &lt;series&gt; element into a YAML series mapping. The XML <c>data</c> attribute
    /// (comma-separated numbers or a <c>{{expression}}</c>) maps to the YAML <c>data</c> key;
    /// the XML <c>points</c> attribute (semicolon-separated x,y[,r] tuples for scatter/bubble)
    /// also maps to <c>data</c> as an array-of-arrays.
    /// </summary>
    private static YamlMappingNode ConvertSeries(XElement series)
    {
        var node = new YamlMappingNode();
        foreach (var attr in series.Attributes())
        {
            var name = attr.Name.LocalName;
            switch (name)
            {
                case "data":
                    node.Add("data", ExpandListAttribute(attr.Value));
                    break;
                case "points":
                    node.Add("data", ExpandListAttribute(attr.Value));
                    break;
                default:
                    node.Add(name, new YamlScalarNode(attr.Value));
                    break;
            }
        }
        return node;
    }
```

(`series` is already in `WrapperNames`; leaving it there is harmless because the new explicit branch intercepts it first. To avoid confusion, remove `"series"` from the `WrapperNames` set and remove the now-dead `case "series":` no-op branch in `AddWrapper`.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlChartTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Build to confirm no dead-code analyzer errors**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Xml/XmlToYamlNodeConverter.cs tests/FlexRender.Tests/Parsing/Xml/XmlChartTests.cs
git commit --no-gpg-sign -m "feat: XML chart series, categories, palette, tuple data"
```

---

### Task 9: KnownProperties validation and typo suggestions flow through XML

Because the XML parser reuses `TemplateParser.ParseElement` → `KnownProperties.Validate`, unknown attributes already raise the same `TemplateParseException` with typo suggestions. Lock this with tests.

**Files:**
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlValidationTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlValidationTests.cs`:

```csharp
using FlexRender.Parsing;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests that XML attribute validation reuses the YAML KnownProperties machinery.
/// </summary>
public class XmlValidationTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_UnknownAttribute_ThrowsWithSuggestion()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <text content="x" colour="#000"/>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("colour", ex.Message, System.StringComparison.Ordinal);
        Assert.Contains("color", ex.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnknownElementType_Throws()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <blob content="x"/>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("Unknown element type", ex.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_CaseMismatchAttribute_HintsCaseSensitivity()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <text content="x" Color="#000"/>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("case-sensitive", ex.Message, System.StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlValidationTests"`
Expected: PASS (3 tests) — validation is inherited from the shared parser. If `Parse_UnknownElementType_Throws` fails because the unknown `<blob>` was silently swallowed, confirm it reaches `ParseElement` (it should, as a layout child).

- [ ] **Step 3: Commit**

```bash
git add tests/FlexRender.Tests/Parsing/Xml/XmlValidationTests.cs
git commit --no-gpg-sign -m "test: XML unknown attribute/element validation and typo hints"
```

---

### Task 10: Resource limits enforced through the XML parser

The shared `TemplateParser` is constructed with the `ResourceLimits` passed to `XmlTemplateParser`, so `MaxShapesPerDraw`, `MaxSeriesPerChart`, and `MaxDataPointsPerSeries` are enforced. Lock this.

**Files:**
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlResourceLimitTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlResourceLimitTests.cs`:

```csharp
using System.Text;
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests that ResourceLimits are enforced by the XML parser via the shared TemplateParser.
/// </summary>
public class XmlResourceLimitTests
{
    [Fact]
    public void Parse_TooManyShapes_Throws()
    {
        var limits = new ResourceLimits { MaxShapesPerDraw = 2 };
        var parser = new XmlTemplateParser(limits);

        var sb = new StringBuilder();
        sb.Append("<flexrender><canvas width=\"300\"/><draw><shapes>");
        for (var i = 0; i < 3; i++)
        {
            sb.Append("<rect x=\"0\" y=\"0\" width=\"1\" height=\"1\"/>");
        }
        sb.Append("</shapes></draw></flexrender>");

        var ex = Assert.Throws<TemplateParseException>(() => parser.Parse(sb.ToString()));
        Assert.Contains("exceeds the maximum", ex.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_TooManySeries_Throws()
    {
        var limits = new ResourceLimits { MaxSeriesPerChart = 1 };
        var parser = new XmlTemplateParser(limits);

        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <chart chart-type="bar">
                <series label="a" data="1,2"/>
                <series label="b" data="3,4"/>
              </chart>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => parser.Parse(xml));
        Assert.Contains("exceeds the maximum", ex.Message, System.StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlResourceLimitTests"`
Expected: PASS (2 tests).

- [ ] **Step 3: Commit**

```bash
git add tests/FlexRender.Tests/Parsing/Xml/XmlResourceLimitTests.cs
git commit --no-gpg-sign -m "test: XML parser enforces resource limits"
```

---

### Task 11: Cross-check — XML and YAML produce equivalent ASTs

Prove a non-trivial template parses identically through both parsers by comparing key AST properties (we compare structurally rather than by reference).

**Files:**
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlYamlEquivalenceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlYamlEquivalenceTests.cs`:

```csharp
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Cross-checks that XML and YAML parsers produce equivalent ASTs for the same template.
/// </summary>
public class XmlYamlEquivalenceTests
{
    private const string Yaml = """
        canvas:
          width: 400
        layout:
          - type: flex
            direction: row
            gap: 8
            children:
              - type: text
                content: "Quarterly sales"
                size: 1.2em
              - type: chart
                chart-type: bar
                categories: [Q1, Q2, Q3, Q4]
                series:
                  - label: "2024"
                    data: [12, 30, 22, 48]
                  - label: "2025"
                    data: [18, 26, 31, 40]
        """;

    private const string Xml = """
        <flexrender>
          <canvas width="400"/>
          <flex direction="row" gap="8">
            <text size="1.2em">Quarterly sales</text>
            <chart chart-type="bar">
              <categories>
                <item>Q1</item><item>Q2</item><item>Q3</item><item>Q4</item>
              </categories>
              <series label="2024" data="12,30,22,48"/>
              <series label="2025" data="18,26,31,40"/>
            </chart>
          </flex>
        </flexrender>
        """;

    [Fact]
    public void XmlAndYaml_ProduceEquivalentAst()
    {
        var fromYaml = new TemplateParser().Parse(Yaml);
        var fromXml = new XmlTemplateParser().Parse(Xml);

        Assert.Equal(fromYaml.Canvas.Width, fromXml.Canvas.Width);

        var yamlFlex = Assert.IsType<FlexElement>(Assert.Single(fromYaml.Elements));
        var xmlFlex = Assert.IsType<FlexElement>(Assert.Single(fromXml.Elements));
        Assert.Equal(yamlFlex.Direction, xmlFlex.Direction);
        Assert.Equal(yamlFlex.Children.Count, xmlFlex.Children.Count);

        var yamlText = Assert.IsType<TextElement>(yamlFlex.Children[0]);
        var xmlText = Assert.IsType<TextElement>(xmlFlex.Children[0]);
        Assert.Equal(yamlText.Content, xmlText.Content);
        Assert.Equal(yamlText.Size.Value, xmlText.Size.Value);

        var yamlChart = Assert.IsType<ChartElement>(yamlFlex.Children[1]);
        var xmlChart = Assert.IsType<ChartElement>(xmlFlex.Children[1]);
        Assert.Equal(yamlChart.ChartType, xmlChart.ChartType);
        Assert.Equal(yamlChart.Categories, xmlChart.Categories);
        Assert.Equal(yamlChart.Series.Count, xmlChart.Series.Count);
        Assert.Equal(yamlChart.Series[0].Label, xmlChart.Series[0].Label);
        Assert.Equal(yamlChart.Series[0].Data, xmlChart.Series[0].Data);
        Assert.Equal(yamlChart.Series[1].Data, xmlChart.Series[1].Data);
    }
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlYamlEquivalenceTests"`
Expected: PASS (1 test). If `gap`/direction differ, recheck the converter attribute pass-through.

- [ ] **Step 3: Commit**

```bash
git add tests/FlexRender.Tests/Parsing/Xml/XmlYamlEquivalenceTests.cs
git commit --no-gpg-sign -m "test: XML/YAML AST equivalence cross-check"
```

---

### Task 12: RenderXml extension methods

Mirror the `RenderYaml` string-based extensions so consumers can render XML directly. These live in `FlexRender.Xml`. They delegate to the same `IFlexRender.Render(Template)` path used by `RenderYaml`. Keep the surface minimal (YAGNI): one `RenderXml(this IFlexRender, string xml, object? data)` returning `byte[]`, matching the primary `RenderYaml` overload.

**Files:**
- Create: `src/FlexRender.Xml/XmlFlexRenderExtensions.cs`
- Test: `tests/FlexRender.Tests/Parsing/Xml/XmlRenderExtensionTests.cs`

- [ ] **Step 1: Inspect the canonical RenderYaml overload to mirror its exact signature**

Read `src/FlexRender.Yaml/FlexRenderExtensions.cs` lines 53-80 (the primary `RenderYaml(this IFlexRender render, string yaml, object? data = null, ... TemplateParser? parser = null, CancellationToken ...)` overload). Mirror its body but: build the `Template` via `XmlToYamlNodeConverter` + the internal `ParseDocumentRoot`, or simply via `new XmlTemplateParser().Parse(xml)`. Use `XmlTemplateParser` to avoid touching internals from the extension. Confirm the downstream call it makes (e.g. `render.RenderToBytes(template, data, ...)` or an internal helper) and reuse the identical downstream call.

- [ ] **Step 2: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Xml/XmlRenderExtensionTests.cs`:

```csharp
using FlexRender.Configuration;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for the RenderXml extension method.
/// </summary>
public class XmlRenderExtensionTests
{
    [Fact]
    public async Task RenderXml_ProducesNonEmptyImage()
    {
        var render = new FlexRenderBuilder()
            .WithSkia()
            .Build();

        const string xml = """
            <flexrender>
              <canvas width="200" height="60"/>
              <text content="Hi" size="1em"/>
            </flexrender>
            """;

        var bytes = await render.RenderXml(xml);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }
}
```

> Confirm `FlexRenderBuilder().WithSkia().Build()` is the correct construction used elsewhere in tests (grep existing tests for `WithSkia()`); adjust if the test project uses a different builder entry point.

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlRenderExtensionTests"`
Expected: FAIL — `RenderXml` does not exist.

- [ ] **Step 4: Implement the extension**

Create `src/FlexRender.Xml/XmlFlexRenderExtensions.cs`. Mirror the exact body of the primary `RenderYaml` overload from Step 1, substituting XML parsing. Template (adjust the downstream render call to match what `RenderYaml` actually calls):

```csharp
using FlexRender.Abstractions;
using FlexRender.Parsing;

namespace FlexRender.Xml;

/// <summary>
/// Convenience extension methods for rendering FlexRender XML templates directly.
/// </summary>
public static class XmlFlexRenderExtensions
{
    /// <summary>
    /// Parses an XML template and renders it to image bytes.
    /// </summary>
    /// <param name="render">The render instance.</param>
    /// <param name="xml">The XML template content.</param>
    /// <param name="data">Optional data context for expression resolution.</param>
    /// <param name="parser">Optional XML parser instance; a default one is created when null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered image bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="xml"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or invalid.</exception>
    public static async Task<byte[]> RenderXml(
        this IFlexRender render,
        string xml,
        object? data = null,
        XmlTemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(xml);

        parser ??= new XmlTemplateParser();
        var template = parser.Parse(xml);

        // Mirror the downstream call performed by RenderYaml. Replace the body below with the
        // identical call RenderYaml makes (e.g. render.RenderAsync(template, data, cancellationToken)).
        return await render.RenderAsync(template, data, cancellationToken).ConfigureAwait(false);
    }
}
```

> CRITICAL: the final `return await render.<...>` MUST be the exact downstream call `RenderYaml` uses (method name + argument order). Copy it verbatim from `FlexRenderExtensions.RenderYaml` (Step 1). Do not invent a method name.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~XmlRenderExtensionTests"`
Expected: PASS (1 test).

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Xml/XmlFlexRenderExtensions.cs tests/FlexRender.Tests/Parsing/Xml/XmlRenderExtensionTests.cs
git commit --no-gpg-sign -m "feat: RenderXml extension method"
```

---

### Task 13: Add FlexRender.Xml to the meta-package

**Files:**
- Modify: `src/FlexRender.MetaPackage/FlexRender.MetaPackage.csproj`

- [ ] **Step 1: Add the project reference**

In `src/FlexRender.MetaPackage/FlexRender.MetaPackage.csproj`, add inside the existing `<ItemGroup>` of `ProjectReference`s, directly after the `FlexRender.Yaml` reference:

```xml
    <ProjectReference Include="..\FlexRender.Xml\FlexRender.Xml.csproj" />
```

- [ ] **Step 2: Build**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/FlexRender.MetaPackage/FlexRender.MetaPackage.csproj
git commit --no-gpg-sign -m "build: include FlexRender.Xml in meta-package"
```

---

### Task 14: Documentation — wiki page, AGENTS.md, llms.txt

**Files:**
- Create: `docs/wiki/Xml-Syntax.md`
- Modify: `AGENTS.md`
- Modify: `llms.txt`
- Modify: `llms-full.txt`

- [ ] **Step 1: Create the wiki page**

Create `docs/wiki/Xml-Syntax.md` documenting the mapping rules and the full side-by-side example from the "XML Syntax Mapping" section of this plan. Include: document shape table, element/attribute rules, the text-inner-text rule, wrapper rules (`then`/`else`/`else-if`/`columns`/`rows`/`series`/`categories`/`palette`/`shapes`), list attribute rules (`data`/`points`), and the flex+chart YAML-vs-XML example. Reuse the exact tables and code blocks from this plan's mapping section verbatim so docs and behaviour stay in lockstep.

- [ ] **Step 2: Update AGENTS.md package structure**

In `AGENTS.md`, locate the package-structure section (the tree that lists `FlexRender.Yaml`). Add a sibling bullet/line for the new parser package immediately under the `FlexRender.Yaml` entry:

```
├── FlexRender.Xml — alternative XML template parser (same Template AST as YAML); RenderXml extension
```

- [ ] **Step 3: Note XML as an alternative input in llms.txt and llms-full.txt**

In `llms.txt`, add a short line in the parsers/usage section: "Templates may be authored in YAML (`FlexRender.Yaml`) or XML (`FlexRender.Xml`, via `XmlTemplateParser` / `RenderXml`). Both produce the same AST." In `llms-full.txt`, add the same note plus the minimal XML example:

```xml
<flexrender>
  <canvas width="300"/>
  <text content="Hello" size="1.5em"/>
</flexrender>
```

- [ ] **Step 4: Commit**

```bash
git add docs/wiki/Xml-Syntax.md AGENTS.md llms.txt llms-full.txt
git commit --no-gpg-sign -m "docs: document XML template syntax"
```

> If `llms.txt` / `llms-full.txt` do not exist at the repo root, search for them (`git ls-files | grep -i llms`) and edit the actual files; if absent, skip those two and note it in the commit body.

---

### Task 15: Final verification — full build and test suite

**Files:** none

- [ ] **Step 1: Full solution build**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 errors, 0 warnings (warnings are errors here).

- [ ] **Step 2: Full XML test suite**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~Parsing.Xml"`
Expected: PASS — all XML parser tests green.

- [ ] **Step 3: Full regression suite (net10.0)**

Run: `dotnet test FlexRender.slnx --framework net10.0`
Expected: PASS — entire suite green (the YAML refactor in Task 2 changed no behaviour).

- [ ] **Step 4: Commit any final cleanups (if needed)**

```bash
git add -A
git commit --no-gpg-sign -m "chore: finalize FlexRender.Xml parser phase 5"
```

---

## Self-Review

**1. Spec coverage (Phase 5: same AST, same elements, parser-level only):**
- New `FlexRender.Xml` package + `ITemplateParser` → Tasks 1, 3.
- Same `Template` AST via shared `TemplateParser.ParseDocumentRoot` → Task 2, proven in Task 11.
- All element types covered: text/separator (T3), flex/qr/barcode/image/content (T4), each/if (T5), table (T6), rect/circle/ellipse/draw + svg/content (T3/T7; svg & content use the generic attribute+inner-text path), chart (T8).
- KnownProperties validation + typo suggestions reused → T9.
- ResourceLimits reused → T10.
- Malformed XML → `TemplateParseException` → T3.
- RenderXml extension + meta-package + DI surface → T12, T13.
- Docs (wiki, AGENTS.md, llms.txt) → T14.
- No new elements, no renderer changes — confirmed: only converter + public parser + extension + docs are new; all element/chart/shape logic is reused.

**2. Placeholder scan:** No "TBD"/"implement later". Two deferred-but-specified items are explicit: the `series`/`shapes` wrapper branches are introduced as no-ops in T3 and filled in T7/T8 with exact code; the `RenderXml` downstream call in T12 is flagged to be copied verbatim from `RenderYaml` (Step 1 reads the exact line) rather than invented — this is the one place the implementer MUST read the canonical signature, because the YAML extension's downstream method name was not load-bearing-verified here.

**3. Type consistency:** `XmlToYamlNodeConverter.Convert(string) -> YamlMappingNode`; `XmlTemplateParser.Parse` calls `_inner.ParseDocumentRoot(root)` (matches the internal method added in T2). `ParseDocumentRoot` is `internal` on `TemplateParser`; `InternalsVisibleTo("FlexRender.Xml")` + `("FlexRender.Tests")` added in T2. `ExpandListAttribute`, `ConvertSeries`, `ConvertShapeSequence`, `AddWrapper`, `WrapperNames`, `ListAttributes` are referenced consistently. AST property names used in tests (`ChartType`, `Categories`, `Series[].Label/.Data/.Points`, `ChartPoint.X/Y`, `DrawElement.Shapes`, `TableElement.Columns/Rows`, `IfElement.ThenBranch/ElseBranch/ElseIf/ConditionPath/Operator/CompareValue`, `EachElement.ItemTemplate/ArrayPath/ItemVariable`) are flagged in each task to be verified against the actual AST and adjusted if they differ.

**Highest-risk tasks:**
- **Task 2** (shared entry point + `InternalsVisibleTo`): refactors the existing YAML `Parse(string)`. Mitigated by running the full YAML parser suite in Step 4. This is the single load-bearing reuse decision.
- **Task 8** (chart series / scatter tuple mapping): the XML `<series>` is a non-wrapper child needing special interception, and the `points` attribute must map to the YAML `data` array-of-arrays the chart parser expects. Confirm `ChartSeries.Points` vs `.Data` semantics for scatter/bubble against `ChartParsers.ParseOneSeries`.
- **Task 12** (`RenderXml` downstream call): must copy the exact render method `RenderYaml` invokes; the YAML extension's downstream signature was not verified in this plan and must be read before implementing.
