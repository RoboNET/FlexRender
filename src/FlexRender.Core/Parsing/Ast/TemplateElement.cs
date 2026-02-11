using FlexRender.Layout;
using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Types of template elements.
/// </summary>
public enum ElementType
{
    /// <summary>
    /// A text element for displaying text content.
    /// </summary>
    Text,

    /// <summary>
    /// An image element for displaying images.
    /// </summary>
    Image,

    /// <summary>
    /// A QR code element.
    /// </summary>
    Qr,

    /// <summary>
    /// A barcode element.
    /// </summary>
    Barcode,

    /// <summary>
    /// A flex container element for layout.
    /// </summary>
    Flex,

    /// <summary>
    /// A separator element for drawing lines.
    /// </summary>
    Separator,

    /// <summary>
    /// An iteration element for looping over arrays.
    /// </summary>
    Each,

    /// <summary>
    /// A conditional element for conditional rendering.
    /// </summary>
    If,

    /// <summary>
    /// A table element for structured tabular data.
    /// </summary>
    Table,

    /// <summary>
    /// An SVG element for rendering vector graphics content.
    /// </summary>
    Svg
}

/// <summary>
/// Base class for all template elements.
/// When adding new properties, update <see cref="CopyBaseProperties"/> below.
/// </summary>
public abstract class TemplateElement
{
    /// <summary>
    /// The type of this element.
    /// </summary>
    public abstract ElementType Type { get; }

    /// <summary>
    /// Rotation of the element.
    /// </summary>
    public ExprValue<string> Rotate { get; set; } = "none";

    /// <summary>
    /// Background color in hex format (e.g., "#000000"). Null means transparent.
    /// </summary>
    public ExprValue<string> Background { get; set; }

    /// <summary>
    /// Padding inside the element (px, %, em). Default is "0".
    /// </summary>
    public ExprValue<string> Padding { get; set; } = "0";

    /// <summary>
    /// Margin outside the element (px, %, em). Default is "0".
    /// </summary>
    public ExprValue<string> Margin { get; set; } = "0";

    /// <summary>
    /// Display mode. None removes the element from layout flow.
    /// </summary>
    public ExprValue<Display> Display { get; set; } = Layout.Display.Flex;

    // Flex item properties (when this element is inside a flex container)

    /// <summary>Flex grow factor.</summary>
    public ExprValue<float> Grow { get; set; }

    /// <summary>Flex shrink factor.</summary>
    public ExprValue<float> Shrink { get; set; } = 1f;

    /// <summary>Flex basis (px, %, em, auto).</summary>
    public ExprValue<string> Basis { get; set; } = "auto";

    /// <summary>Self alignment override.</summary>
    public ExprValue<AlignSelf> AlignSelf { get; set; } = Layout.AlignSelf.Auto;

    /// <summary>Display order.</summary>
    public ExprValue<int> Order { get; set; }

    /// <summary>Width (px, %, em, auto).</summary>
    public ExprValue<string> Width { get; set; }

    /// <summary>Height (px, %, em, auto).</summary>
    public ExprValue<string> Height { get; set; }

    /// <summary>Minimum width constraint (px, %, em).</summary>
    public ExprValue<string> MinWidth { get; set; }

    /// <summary>Maximum width constraint (px, %, em).</summary>
    public ExprValue<string> MaxWidth { get; set; }

    /// <summary>Minimum height constraint (px, %, em).</summary>
    public ExprValue<string> MinHeight { get; set; }

    /// <summary>Maximum height constraint (px, %, em).</summary>
    public ExprValue<string> MaxHeight { get; set; }

    /// <summary>Positioning mode.</summary>
    public ExprValue<Position> Position { get; set; } = Layout.Position.Static;

    /// <summary>Top inset for positioned elements.</summary>
    public ExprValue<string> Top { get; set; }

    /// <summary>Right inset for positioned elements.</summary>
    public ExprValue<string> Right { get; set; }

    /// <summary>Bottom inset for positioned elements.</summary>
    public ExprValue<string> Bottom { get; set; }

    /// <summary>Left inset for positioned elements.</summary>
    public ExprValue<string> Left { get; set; }

    /// <summary>Aspect ratio (width / height). When one dimension is known, the other is computed.</summary>
    public ExprValue<float?> AspectRatio { get; set; }

    // Border properties

    /// <summary>Border shorthand: "width style color" (e.g., "2 solid #333"). Applies to all sides.</summary>
    public ExprValue<string> Border { get; set; }

    /// <summary>Border width (px, em). Overrides shorthand width on all sides.</summary>
    public ExprValue<string> BorderWidth { get; set; }

    /// <summary>Border color in hex format. Overrides shorthand color on all sides.</summary>
    public ExprValue<string> BorderColor { get; set; }

    /// <summary>Border style: solid, dashed, dotted, none. Overrides shorthand style on all sides.</summary>
    public ExprValue<string> BorderStyle { get; set; }

    /// <summary>Per-side border shorthand for the top side: "width style color".</summary>
    public ExprValue<string> BorderTop { get; set; }

    /// <summary>Per-side border shorthand for the right side: "width style color".</summary>
    public ExprValue<string> BorderRight { get; set; }

    /// <summary>Per-side border shorthand for the bottom side: "width style color".</summary>
    public ExprValue<string> BorderBottom { get; set; }

    /// <summary>Per-side border shorthand for the left side: "width style color".</summary>
    public ExprValue<string> BorderLeft { get; set; }

    /// <summary>Border radius for corner rounding (px, em, %).</summary>
    public ExprValue<string> BorderRadius { get; set; }

    /// <summary>
    /// Text direction override. Null means inherit from parent/canvas.
    /// </summary>
    public ExprValue<TextDirection?> TextDirection { get; set; }

    /// <summary>
    /// Opacity of the element (0.0 = fully transparent, 1.0 = fully opaque).
    /// Affects the entire element including children (CSS behavior).
    /// Only allocates an offscreen buffer when less than 1.0.
    /// </summary>
    public ExprValue<float> Opacity { get; set; } = 1.0f;

    /// <summary>
    /// Box shadow definition: "offsetX offsetY blurRadius color".
    /// Example: "4 4 8 rgba(0,0,0,0.3)".
    /// Null means no shadow.
    /// </summary>
    public ExprValue<string> BoxShadow { get; set; }

    /// <summary>
    /// Resolves template expressions in all <see cref="ExprValue{T}"/> properties on this element.
    /// Override in derived classes to resolve subclass-specific properties.
    /// </summary>
    /// <param name="resolver">Function that resolves a raw template string to a concrete string value.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    public virtual void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        Rotate = Rotate.Resolve(resolver, data);
        Background = Background.Resolve(resolver, data);
        Padding = Padding.Resolve(resolver, data);
        Margin = Margin.Resolve(resolver, data);
        Grow = Grow.Resolve(resolver, data);
        Shrink = Shrink.Resolve(resolver, data);
        Basis = Basis.Resolve(resolver, data);
        Order = Order.Resolve(resolver, data);
        Display = Display.Resolve(resolver, data);
        AlignSelf = AlignSelf.Resolve(resolver, data);
        Opacity = Opacity.Resolve(resolver, data);
        Width = Width.Resolve(resolver, data);
        Height = Height.Resolve(resolver, data);
        MinWidth = MinWidth.Resolve(resolver, data);
        MaxWidth = MaxWidth.Resolve(resolver, data);
        MinHeight = MinHeight.Resolve(resolver, data);
        MaxHeight = MaxHeight.Resolve(resolver, data);
        Position = Position.Resolve(resolver, data);
        Top = Top.Resolve(resolver, data);
        Right = Right.Resolve(resolver, data);
        Bottom = Bottom.Resolve(resolver, data);
        Left = Left.Resolve(resolver, data);
        AspectRatio = AspectRatio.Resolve(resolver, data);
        TextDirection = TextDirection.Resolve(resolver, data);
        BoxShadow = BoxShadow.Resolve(resolver, data);
        Border = Border.Resolve(resolver, data);
        BorderWidth = BorderWidth.Resolve(resolver, data);
        BorderColor = BorderColor.Resolve(resolver, data);
        BorderStyle = BorderStyle.Resolve(resolver, data);
        BorderTop = BorderTop.Resolve(resolver, data);
        BorderRight = BorderRight.Resolve(resolver, data);
        BorderBottom = BorderBottom.Resolve(resolver, data);
        BorderLeft = BorderLeft.Resolve(resolver, data);
        BorderRadius = BorderRadius.Resolve(resolver, data);
    }

    /// <summary>
    /// Materializes all resolved <see cref="ExprValue{T}"/> properties into typed values.
    /// Override in derived classes to materialize subclass-specific properties.
    /// </summary>
    public virtual void Materialize()
    {
        Rotate = Rotate.Materialize(nameof(Rotate));
        Background = Background.Materialize(nameof(Background), ValueKind.Color);
        Padding = Padding.Materialize(nameof(Padding), ValueKind.Size);
        Margin = Margin.Materialize(nameof(Margin), ValueKind.Size);
        Grow = Grow.Materialize(nameof(Grow));
        Shrink = Shrink.Materialize(nameof(Shrink));
        Basis = Basis.Materialize(nameof(Basis));
        Order = Order.Materialize(nameof(Order));
        Display = Display.Materialize(nameof(Display));
        AlignSelf = AlignSelf.Materialize(nameof(AlignSelf));
        Opacity = Opacity.Materialize(nameof(Opacity));
        Width = Width.Materialize(nameof(Width), ValueKind.Size);
        Height = Height.Materialize(nameof(Height), ValueKind.Size);
        MinWidth = MinWidth.Materialize(nameof(MinWidth), ValueKind.Size);
        MaxWidth = MaxWidth.Materialize(nameof(MaxWidth), ValueKind.Size);
        MinHeight = MinHeight.Materialize(nameof(MinHeight), ValueKind.Size);
        MaxHeight = MaxHeight.Materialize(nameof(MaxHeight), ValueKind.Size);
        Position = Position.Materialize(nameof(Position));
        Top = Top.Materialize(nameof(Top), ValueKind.Size);
        Right = Right.Materialize(nameof(Right), ValueKind.Size);
        Bottom = Bottom.Materialize(nameof(Bottom), ValueKind.Size);
        Left = Left.Materialize(nameof(Left), ValueKind.Size);
        AspectRatio = AspectRatio.Materialize(nameof(AspectRatio));
        TextDirection = TextDirection.Materialize(nameof(TextDirection));
        BoxShadow = BoxShadow.Materialize(nameof(BoxShadow));
        Border = Border.Materialize(nameof(Border));
        BorderWidth = BorderWidth.Materialize(nameof(BorderWidth));
        BorderColor = BorderColor.Materialize(nameof(BorderColor), ValueKind.Color);
        BorderStyle = BorderStyle.Materialize(nameof(BorderStyle));
        BorderTop = BorderTop.Materialize(nameof(BorderTop));
        BorderRight = BorderRight.Materialize(nameof(BorderRight));
        BorderBottom = BorderBottom.Materialize(nameof(BorderBottom));
        BorderLeft = BorderLeft.Materialize(nameof(BorderLeft));
        BorderRadius = BorderRadius.Materialize(nameof(BorderRadius));
    }

    /// <summary>
    /// Copies all base flex-item and positioning properties from source to target element.
    /// Properties that require per-element transformation (Background, Rotate, Padding, Margin)
    /// are intentionally excluded and must be set by each caller.
    /// </summary>
    /// <param name="source">The source element to copy properties from.</param>
    /// <param name="target">The target element to copy properties to.</param>
    public static void CopyBaseProperties(TemplateElement source, TemplateElement target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        // Flex-item properties
        target.Grow = source.Grow;
        target.Shrink = source.Shrink;
        target.Basis = source.Basis;
        target.AlignSelf = source.AlignSelf;
        target.Order = source.Order;
        target.Width = source.Width;
        target.Height = source.Height;
        target.MinWidth = source.MinWidth;
        target.MaxWidth = source.MaxWidth;
        target.MinHeight = source.MinHeight;
        target.MaxHeight = source.MaxHeight;

        // Position properties
        target.Position = source.Position;
        target.Top = source.Top;
        target.Right = source.Right;
        target.Bottom = source.Bottom;
        target.Left = source.Left;

        // Other base properties
        target.Display = source.Display;
        target.AspectRatio = source.AspectRatio;

        // Border properties
        target.Border = source.Border;
        target.BorderWidth = source.BorderWidth;
        target.BorderColor = source.BorderColor;
        target.BorderStyle = source.BorderStyle;
        target.BorderTop = source.BorderTop;
        target.BorderRight = source.BorderRight;
        target.BorderBottom = source.BorderBottom;
        target.BorderLeft = source.BorderLeft;
        target.BorderRadius = source.BorderRadius;

        // Text direction
        target.TextDirection = source.TextDirection;

        // Visual effects
        target.Opacity = source.Opacity;
        target.BoxShadow = source.BoxShadow;
    }
}
