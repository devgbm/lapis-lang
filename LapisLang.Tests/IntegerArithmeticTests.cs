using LapisLang.Core;

namespace LapisLang.Tests;

public class IntegerArithmeticTests
{
    [Fact]
    public void Addition() => Assert.Equal(3L, H.EvalAs<IntegerSymbol>("1 + 2").Value);

    [Fact]
    public void Subtraction() => Assert.Equal(7L, H.EvalAs<IntegerSymbol>("10 - 3").Value);

    [Fact]
    public void Multiplication() => Assert.Equal(20L, H.EvalAs<IntegerSymbol>("4 * 5").Value);

    [Fact]
    public void Division() => Assert.Equal(5L, H.EvalAs<IntegerSymbol>("10 / 2").Value);

    [Fact]
    public void Modulo() => Assert.Equal(1L, H.EvalAs<IntegerSymbol>("10 % 3").Value);

    [Fact]
    public void Modulo_Zero_Remainder() => Assert.Equal(0L, H.EvalAs<IntegerSymbol>("9 % 3").Value);

    [Fact]
    public void Division_By_Zero_Positive_Returns_MaxValue()
    {
        var result = H.EvalAs<IntegerSymbol>("5 / 0");
        Assert.Equal(long.MaxValue, result.Value);
    }

    [Fact]
    public void Division_By_Zero_Negative_Returns_Negative_MaxValue()
    {
        var result = H.EvalAs<IntegerSymbol>("-5 / 0");
        Assert.Equal(-long.MaxValue, result.Value);
    }

    [Fact]
    public void Chained_Addition() => Assert.Equal(6L, H.EvalAs<IntegerSymbol>("1 + 2 + 3").Value);

    [Fact]
    public void Multiplication_Has_Higher_Precedence_Than_Addition()
    {
        Assert.Equal(7L, H.EvalAs<IntegerSymbol>("1 + 2 * 3").Value);
    }

    [Fact]
    public void Parentheses_Override_Precedence()
    {
        Assert.Equal(9L, H.EvalAs<IntegerSymbol>("(1 + 2) * 3").Value);
    }

    [Fact]
    public void Subtraction_Is_Left_Associative()
    {
        Assert.Equal(2L, H.EvalAs<IntegerSymbol>("7 - 3 - 2").Value);
    }

    [Fact]
    public void Arithmetic_Result_Has_Integer_Type()
    {
        Assert.Equal(LangDefaults.Types.Integer, H.EvalAs<IntegerSymbol>("1 + 1").Type);
    }
}
