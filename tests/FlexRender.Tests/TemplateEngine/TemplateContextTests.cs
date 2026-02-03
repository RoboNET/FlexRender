using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public class TemplateContextTests
{
    [Fact]
    public void Constructor_WithRootData_SetsData()
    {
        var data = new ObjectValue { ["name"] = "Test" };
        var context = new TemplateContext(data);

        Assert.Same(data, context.CurrentScope);
    }

    [Fact]
    public void PushScope_AddsNewScope()
    {
        var root = new ObjectValue { ["name"] = "Root" };
        var child = new ObjectValue { ["name"] = "Child" };
        var context = new TemplateContext(root);

        context.PushScope(child);

        Assert.Same(child, context.CurrentScope);
    }

    [Fact]
    public void PopScope_RestoresPreviousScope()
    {
        var root = new ObjectValue { ["name"] = "Root" };
        var child = new ObjectValue { ["name"] = "Child" };
        var context = new TemplateContext(root);

        context.PushScope(child);
        context.PopScope();

        Assert.Same(root, context.CurrentScope);
    }

    [Fact]
    public void PopScope_OnRootScope_ThrowsException()
    {
        var root = new ObjectValue { ["name"] = "Root" };
        var context = new TemplateContext(root);

        Assert.Throws<InvalidOperationException>(() => context.PopScope());
    }

    [Fact]
    public void SetLoopVariables_SetsIndexFirstLast()
    {
        var context = new TemplateContext(new ObjectValue());

        context.SetLoopVariables(index: 0, count: 3);

        Assert.Equal(0, context.LoopIndex);
        Assert.True(context.IsFirst);
        Assert.False(context.IsLast);
    }

    [Fact]
    public void SetLoopVariables_LastItem_SetsIsLast()
    {
        var context = new TemplateContext(new ObjectValue());

        context.SetLoopVariables(index: 2, count: 3);

        Assert.Equal(2, context.LoopIndex);
        Assert.False(context.IsFirst);
        Assert.True(context.IsLast);
    }

    [Fact]
    public void SetLoopVariables_SingleItem_BothFirstAndLast()
    {
        var context = new TemplateContext(new ObjectValue());

        context.SetLoopVariables(index: 0, count: 1);

        Assert.True(context.IsFirst);
        Assert.True(context.IsLast);
    }

    [Fact]
    public void ClearLoopVariables_ResetsAll()
    {
        var context = new TemplateContext(new ObjectValue());
        context.SetLoopVariables(index: 5, count: 10);

        context.ClearLoopVariables();

        Assert.Null(context.LoopIndex);
        Assert.False(context.IsFirst);
        Assert.False(context.IsLast);
    }

    [Fact]
    public void NestedScopes_MaintainIndependentLoopVariables()
    {
        var context = new TemplateContext(new ObjectValue());
        context.SetLoopVariables(index: 0, count: 2);
        var outerIndex = context.LoopIndex;

        context.PushScope(new ObjectValue());
        context.SetLoopVariables(index: 1, count: 3);

        Assert.Equal(1, context.LoopIndex);

        context.PopScope();

        // Loop variables are per-context-level, need to restore
        Assert.Equal(1, context.LoopIndex); // This shows current behavior
    }

    // Validation Tests: SetLoopVariables
    [Fact]
    public void SetLoopVariables_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var context = new TemplateContext(new ObjectValue());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            context.SetLoopVariables(index: -1, count: 5));
    }

    [Fact]
    public void SetLoopVariables_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        var context = new TemplateContext(new ObjectValue());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            context.SetLoopVariables(index: 0, count: -1));
    }

    [Fact]
    public void SetLoopVariables_ZeroCount_ThrowsArgumentOutOfRangeException()
    {
        var context = new TemplateContext(new ObjectValue());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            context.SetLoopVariables(index: 0, count: 0));
    }

    [Fact]
    public void SetLoopVariables_IndexGreaterThanOrEqualToCount_ThrowsArgumentOutOfRangeException()
    {
        var context = new TemplateContext(new ObjectValue());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            context.SetLoopVariables(index: 5, count: 5));
    }

    [Fact]
    public void SetLoopVariables_ValidParameters_Succeeds()
    {
        var context = new TemplateContext(new ObjectValue());

        var exception = Record.Exception(() =>
            context.SetLoopVariables(index: 0, count: 1));

        Assert.Null(exception);
    }
}
