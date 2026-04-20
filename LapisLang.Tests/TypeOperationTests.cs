using LapisLang.Core;

namespace LapisLang.Tests;

public class TypeOperationTests
{
    [Fact]
    public void TypeWith_Combines_Fields()
    {
        var result = (StructTypeSymbol)H.Eval("type { a: Int; } & type { b: Bool; }");
        Assert.Equal(2, result.Fields.Length);
        Assert.Contains(result.Fields, f => f.Name == "a");
        Assert.Contains(result.Fields, f => f.Name == "b");
    }

    [Fact]
    public void TypeWith_Deduplicates_Common_Fields()
    {
        var result = (StructTypeSymbol)H.Eval("type { a: Int; b: Bool; } & type { b: Bool; c: Str; }");
        Assert.Equal(3, result.Fields.Length);
    }

    [Fact]
    public void TypeWithout_Removes_Fields()
    {
        var result = (StructTypeSymbol)H.Eval("type { a: Int; b: Bool; } !& type { b: Bool; }");
        var field = Assert.Single(result.Fields);
        Assert.Equal("a", field.Name);
    }

    // `has` keyword not yet wired into the expression parser precedence table
}
