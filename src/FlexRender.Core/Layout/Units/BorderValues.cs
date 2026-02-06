namespace FlexRender.Layout.Units;

/// <summary>
/// Border line styles matching CSS border-style values.
/// </summary>
public enum BorderLineStyle
{
    /// <summary>No border is drawn.</summary>
    None,

    /// <summary>A solid line border.</summary>
    Solid,

    /// <summary>A dashed line border.</summary>
    Dashed,

    /// <summary>A dotted line border.</summary>
    Dotted
}

/// <summary>
/// Represents a resolved border for one side.
/// </summary>
/// <param name="Width">The border width in pixels.</param>
/// <param name="Style">The border line style.</param>
/// <param name="Color">The border color as a CSS color string.</param>
public readonly record struct BorderSide(float Width, BorderLineStyle Style, string Color)
{
    /// <summary>
    /// A border side with zero width and no style.
    /// </summary>
    public static readonly BorderSide None = new(0f, BorderLineStyle.None, "#000000");

    /// <summary>
    /// Whether this border side is visible (has width and a visible style).
    /// </summary>
    public bool IsVisible => Width > 0f && Style != BorderLineStyle.None;
}

/// <summary>
/// Resolved border values for all four sides of an element.
/// </summary>
/// <param name="Top">The top border side.</param>
/// <param name="Right">The right border side.</param>
/// <param name="Bottom">The bottom border side.</param>
/// <param name="Left">The left border side.</param>
public readonly record struct BorderValues(BorderSide Top, BorderSide Right, BorderSide Bottom, BorderSide Left)
{
    /// <summary>
    /// A border with all sides set to none.
    /// </summary>
    public static readonly BorderValues Zero = new(BorderSide.None, BorderSide.None, BorderSide.None, BorderSide.None);

    /// <summary>
    /// Total horizontal border width (left + right).
    /// </summary>
    public float Horizontal => Left.Width + Right.Width;

    /// <summary>
    /// Total vertical border width (top + bottom).
    /// </summary>
    public float Vertical => Top.Width + Bottom.Width;

    /// <summary>
    /// Whether any border side is visible.
    /// </summary>
    public bool HasVisibleBorder => Top.IsVisible || Right.IsVisible || Bottom.IsVisible || Left.IsVisible;
}
