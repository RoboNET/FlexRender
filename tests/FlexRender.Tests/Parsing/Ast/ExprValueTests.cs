using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

public sealed class ExprValueTests
{
    // === Construction ===

    [Fact]
    public void FromLiteral_Float_StoresValue()
    {
        ExprValue<float> v = 0.5f; // implicit operator
        Assert.Equal(0.5f, v.Value);
        Assert.False(v.IsExpression);
        Assert.Null(v.RawValue);
    }

    [Fact]
    public void FromLiteral_String_StoresValue()
    {
        ExprValue<string> v = "hello";
        Assert.Equal("hello", v.Value);
        Assert.False(v.IsExpression);
        Assert.Null(v.RawValue);
    }

    [Fact]
    public void FromLiteral_Enum_StoresValue()
    {
        ExprValue<TextAlign> v = TextAlign.Center;
        Assert.Equal(TextAlign.Center, v.Value);
        Assert.False(v.IsExpression);
    }

    [Fact]
    public void FromLiteral_NullableInt_StoresNull()
    {
        var v = new ExprValue<int?>((int?)null);
        Assert.Null(v.Value);
        Assert.False(v.IsExpression);
    }

    [Fact]
    public void FromExpression_StoresRawAndFlag()
    {
        var v = ExprValue<float>.Expression("{{theme.opacity}}");
        Assert.Equal("{{theme.opacity}}", v.RawValue);
        Assert.True(v.IsExpression);
        Assert.False(v.IsResolved);
    }

    [Fact]
    public void FromRawLiteral_StoresRawAndValue()
    {
        var v = ExprValue<float>.RawLiteral("0.5", 0.5f);
        Assert.Equal("0.5", v.RawValue);
        Assert.Equal(0.5f, v.Value);
        Assert.False(v.IsExpression);
    }

    [Fact]
    public void Default_HasDefaultValue()
    {
        var v = default(ExprValue<float>);
        Assert.Equal(0f, v.Value);
        Assert.False(v.IsExpression);
        Assert.False(v.IsResolved);
    }

    // === Resolve ===

    [Fact]
    public void Resolve_Expression_CallsResolver()
    {
        var v = ExprValue<float>.Expression("{{x}}");
        var resolved = v.Resolve((raw, _) => "0.7", null!);
        Assert.Equal("0.7", resolved.RawValue);
        Assert.False(resolved.IsExpression);
        Assert.False(resolved.IsResolved); // not yet materialized
    }

    [Fact]
    public void Resolve_Literal_PassesThrough()
    {
        ExprValue<float> v = 0.5f;
        var resolved = v.Resolve((_, _) => throw new InvalidOperationException("should not call"), null!);
        Assert.Equal(0.5f, resolved.Value);
    }

    [Fact]
    public void Resolve_RawLiteral_PassesThrough()
    {
        var v = ExprValue<float>.RawLiteral("0.5", 0.5f);
        var resolved = v.Resolve((_, _) => throw new InvalidOperationException("should not call"), null!);
        Assert.Equal(0.5f, resolved.Value);
    }

    // === Materialize ===

    [Fact]
    public void Materialize_AlreadyResolved_PassesThrough()
    {
        ExprValue<float> v = 0.5f;
        var materialized = v.Materialize(propertyName: "Opacity");
        Assert.Equal(0.5f, materialized.Value);
        Assert.True(materialized.IsResolved);
    }

    [Fact]
    public void Materialize_ResolvedExpression_ParsesFloat()
    {
        var v = ExprValue<float>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "0.8", null!);
        var materialized = resolved.Materialize(propertyName: "Opacity");
        Assert.Equal(0.8f, materialized.Value);
        Assert.True(materialized.IsResolved);
    }

    [Fact]
    public void Materialize_InvalidFloat_Throws()
    {
        var v = ExprValue<float>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "hello", null!);
        var ex = Assert.Throws<TemplateEngineException>(() => resolved.Materialize("Opacity"));
        Assert.Contains("Opacity", ex.Message);
        Assert.Contains("hello", ex.Message);
    }

    [Fact]
    public void Materialize_NullableInt_EmptyRaw_ReturnsNull()
    {
        var v = ExprValue<int?>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "", null!);
        var materialized = resolved.Materialize("MaxLines");
        Assert.Null(materialized.Value);
        Assert.True(materialized.IsResolved);
    }

    [Fact]
    public void Materialize_Bool_ParsesTrue()
    {
        var v = ExprValue<bool>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "true", null!);
        var materialized = resolved.Materialize("ShowText");
        Assert.True(materialized.Value);
    }

    [Fact]
    public void Materialize_NullableFloat_ParsesValue()
    {
        var v = ExprValue<float?>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "1.5", null!);
        var materialized = resolved.Materialize("AspectRatio");
        Assert.Equal(1.5f, materialized.Value);
    }

    [Fact]
    public void Materialize_String_PassesThrough()
    {
        var v = ExprValue<string>.Expression("{{name}}");
        var resolved = v.Resolve((_, _) => "Alice", null!);
        var materialized = resolved.Materialize("Content");
        Assert.Equal("Alice", materialized.Value);
        Assert.True(materialized.IsResolved);
    }

    // === Enum ===

    [Fact]
    public void Materialize_Enum_ParsesValue()
    {
        var v = ExprValue<TextAlign>.Expression("{{align}}");
        var resolved = v.Resolve((_, _) => "center", null!);
        var materialized = resolved.Materialize("Align");
        Assert.Equal(TextAlign.Center, materialized.Value);
        Assert.True(materialized.IsResolved);
    }

    [Fact]
    public void Materialize_Enum_InvalidValue_Throws()
    {
        var v = ExprValue<TextAlign>.Expression("{{align}}");
        var resolved = v.Resolve((_, _) => "banana", null!);
        var ex = Assert.Throws<TemplateEngineException>(() => resolved.Materialize("Align"));
        Assert.Contains("Align", ex.Message);
        Assert.Contains("banana", ex.Message);
        Assert.Contains("Left", ex.Message); // valid values listed
    }

    // === ToString ===

    [Fact]
    public void ToString_Expression_ShowsExpr()
    {
        var v = ExprValue<float>.Expression("{{x}}");
        Assert.Equal("Expr({{x}})", v.ToString());
    }

    [Fact]
    public void ToString_Literal_ShowsValue()
    {
        ExprValue<float> v = 0.5f;
        Assert.Equal("0.5", v.ToString());
    }

    // === Additional edge cases ===

    [Fact]
    public void Materialize_Double_ParsesValue()
    {
        var v = ExprValue<double>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "3.14", null!);
        var materialized = resolved.Materialize("Ratio");
        Assert.Equal(3.14, materialized.Value);
    }

    [Fact]
    public void Materialize_Int_ParsesValue()
    {
        var v = ExprValue<int>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "42", null!);
        var materialized = resolved.Materialize("Order");
        Assert.Equal(42, materialized.Value);
    }

    [Fact]
    public void Materialize_Int_InvalidValue_Throws()
    {
        var v = ExprValue<int>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "3.14", null!);
        var ex = Assert.Throws<TemplateEngineException>(() => resolved.Materialize("Order"));
        Assert.Contains("Order", ex.Message);
        Assert.Contains("3.14", ex.Message);
    }

    [Fact]
    public void Materialize_EmptyStringForNonNullableFloat_Throws()
    {
        var v = ExprValue<float>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "", null!);
        var ex = Assert.Throws<TemplateEngineException>(() => resolved.Materialize("Opacity"));
        Assert.Contains("Opacity", ex.Message);
    }

    [Fact]
    public void Materialize_EmptyStringForString_ReturnsEmpty()
    {
        var v = ExprValue<string>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "", null!);
        var materialized = resolved.Materialize("Content");
        Assert.Equal("", materialized.Value);
    }

    [Fact]
    public void Materialize_NullableDouble_ParsesValue()
    {
        var v = ExprValue<double?>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "2.718", null!);
        var materialized = resolved.Materialize("Ratio");
        Assert.Equal(2.718, materialized.Value);
    }

    [Fact]
    public void Materialize_NullableBool_ParsesValue()
    {
        var v = ExprValue<bool?>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "false", null!);
        var materialized = resolved.Materialize("Visible");
        Assert.Equal(false, materialized.Value);
    }

    [Fact]
    public void Materialize_NullableDouble_EmptyRaw_ReturnsNull()
    {
        var v = ExprValue<double?>.Expression("{{x}}");
        var resolved = v.Resolve((_, _) => "", null!);
        var materialized = resolved.Materialize("Ratio");
        Assert.Null(materialized.Value);
        Assert.True(materialized.IsResolved);
    }

    [Fact]
    public void ToString_RawLiteral_ShowsRawAndValue()
    {
        var v = ExprValue<float>.RawLiteral("0.5", 0.5f);
        Assert.Equal("Raw(0.5)=0.5", v.ToString());
    }
}
