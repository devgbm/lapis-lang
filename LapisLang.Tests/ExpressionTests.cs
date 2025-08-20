using System;
using System.Collections;
using System.Collections.Generic;
using LapisLang.Core;

namespace LapisLang.Tests;

public class ExpressionTests
{
    [Fact]
    public void Type_Is_A_Primitive_Type()
    {
        var type = LangDefaults.Types.Type;
        Assert.Equal(type.Type, type);
    }
}