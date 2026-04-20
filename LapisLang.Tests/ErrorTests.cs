using System;
using System.Collections;
using System.Collections.Generic;
using LapisLang.Core;

namespace LapisLang.Tests;

public class ErrorTests
{
    [Fact]
    public void Unknown_Variable_Produces_Error()
    {
        Assert.True(H.HasErrors("nonExistentVar"));
    }

    [Fact]
    public void Adding_Int_And_Bool_Produces_Error()
    {
        Assert.True(H.HasErrors("1 + true"));
    }

    [Fact]
    public void Calling_Non_Function_Produces_Error()
    {
        Assert.True(H.HasErrors("42(1)"));
    }

    [Fact]
    public void Wrong_Argument_Count_Produces_Error()
    {
        Assert.True(H.HasErrors("(func (Int a) Int: { return a; })(1, 2)"));
    }

    [Fact]
    public void Wrong_Argument_Type_Produces_Error()
    {
        Assert.True(H.HasErrors("(func (Int a) Int: { return a; })(true)"));
    }

    [Fact]
    public void If_With_Non_Boolean_Condition_Produces_Error()
    {
        Assert.True(H.HasErrors("(func () Int: { if (1) { return 1; } else { return 2; } })()"));
    }

    [Fact]
    public void Redefining_Constant_Produces_Error()
    {
        var scope = new EvaluationScope();
        H.Eval("def x = 1;", scope);
        Assert.True(H.HasErrors("def x = 2;", scope));
    }

    [Fact]
    public void Parsing_Invalid_Code_Produces_Error()
    {
        Assert.True(H.HasErrors("def = ;"));
    }
}
