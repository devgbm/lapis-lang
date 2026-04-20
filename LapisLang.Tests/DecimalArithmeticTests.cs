using LapisLang.Core;

namespace LapisLang.Tests;

public class DecimalArithmeticTests
{
    [Fact]
    public void Addition() => Assert.Equal(4.0m, H.EvalAs<DecimalSymbol>("1.5 + 2.5").Value);

    [Fact]
    public void Subtraction() => Assert.Equal(2.5m, H.EvalAs<DecimalSymbol>("5.0 - 2.5").Value);

    [Fact]
    public void Multiplication() => Assert.Equal(6.0m, H.EvalAs<DecimalSymbol>("2.0 * 3.0").Value);

    [Fact]
    public void Division() => Assert.Equal(3.0m, H.EvalAs<DecimalSymbol>("9.0 / 3.0").Value);

    [Fact]
    public void Modulo() => Assert.Equal(0.5m, H.EvalAs<DecimalSymbol>("2.5 % 1.0").Value);
}
