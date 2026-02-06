# justify Property Visual Reference

The `justify` property defines how flex items are aligned along the main axis of a flex container. It distributes extra free space when items don't fill the entire container width (for `direction: row`) or height (for `direction: column`).

## Values

| Value | Description | Visual Example |
|-------|-------------|----------------|
| `start` | Items are packed toward the start of the flex container's main axis. This is the default behavior. | ![justify: start](../../examples/visual-docs/output/justify-start.png) |
| `center` | Items are centered along the main axis, with equal space on both sides. | ![justify: center](../../examples/visual-docs/output/justify-center.png) |
| `end` | Items are packed toward the end of the flex container's main axis. | ![justify: end](../../examples/visual-docs/output/justify-end.png) |
| `space-between` | Items are evenly distributed with the first item at the start and the last item at the end. Remaining space is distributed evenly between items. | ![justify: space-between](../../examples/visual-docs/output/justify-space-between.png) |
| `space-around` | Items are evenly distributed with equal space around each item. Note that the space at the edges is half the space between items. | ![justify: space-around](../../examples/visual-docs/output/justify-space-around.png) |
| `space-evenly` | Items are evenly distributed with equal space between all items and at the edges. | ![justify: space-evenly](../../examples/visual-docs/output/justify-space-evenly.png) |

## Code Examples

### YAML Template

```yaml
layout:
  - type: flex
    direction: row
    justify: center
    children:
      - type: text
        content: "Centered Item"
```

### AST (C# Code)

```csharp
using FlexRender.Layout;
using FlexRender.Parsing.Ast;

var template = new Template
{
    Canvas = new CanvasSettings { Width = 400, Fixed = FixedDimension.Width },
    Elements = new List<TemplateElement>
    {
        new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.Center,
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "Centered Item" }
            }
        }
    }
};
```

## Notes

- The `justify` property only affects items when there is free space available along the main axis.
- For `direction: row`, justify controls horizontal alignment.
- For `direction: column`, justify controls vertical alignment.
- This property has no effect on items that take up all available space.

## See Also

- [[Flexbox-Layout]] - Complete flexbox layout reference
- [[Element-Reference]] - All available element types and properties
