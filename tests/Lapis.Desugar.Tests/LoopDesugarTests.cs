using Lapis.Ast.Core;

namespace Lapis.Desugar.Tests;

/// <summary>
/// <c>loop</c>/<c>break</c>/<c>continue</c> (plano 26, M16 — Q32).
///
/// Estrutural, sem decomposição em blocos básicos: cada nó de Surface vira o nó
/// de Core correspondente diretamente — o que substitui inteiramente a
/// decomposição em grupos de join points do plano 16 §16.4 (`GotoDesugarTests`,
/// retirado).
/// </summary>
public sealed class LoopDesugarTests : DesugarTestBase
{
    [Fact]
    public void Loop_Unlabeled_DesugarsToCoreLoop()
    {
        var loop = AllNodes(Compile("loop { break; };")).OfType<CoreLoop>().ShouldHaveSingleItem();

        loop.Label.ShouldBeNull();
    }

    [Fact]
    public void Loop_Labeled_KeepsTheLabel()
    {
        var loop = AllNodes(Compile("loop :fora { break; };")).OfType<CoreLoop>().ShouldHaveSingleItem();

        loop.Label.ShouldBe("fora");
    }

    [Fact]
    public void Break_Bare_HasNoValue()
    {
        var jump = AllNodes(Compile("loop { break; };")).OfType<CoreBreak>().ShouldHaveSingleItem();

        jump.Label.ShouldBeNull();
        jump.Value.ShouldBeNull();
    }

    [Fact]
    public void Break_WithValue_DesugarsTheValue()
    {
        var jump = AllNodes(Compile("loop { break 1 + 2; };")).OfType<CoreBreak>().ShouldHaveSingleItem();

        jump.Value.ShouldBeOfType<CoreBinary>();
    }

    [Fact]
    public void Break_WithLabel_KeepsIt()
    {
        var jump = AllNodes(Compile("loop :fora { break :fora; };")).OfType<CoreBreak>().ShouldHaveSingleItem();

        jump.Label.ShouldBe("fora");
    }

    [Fact]
    public void Continue_Bare_DesugarsToCoreContinue() =>
        AllNodes(Compile("loop { continue; };")).OfType<CoreContinue>().ShouldHaveSingleItem().Label.ShouldBeNull();

    [Fact]
    public void Continue_WithLabel_KeepsIt() =>
        AllNodes(Compile("loop :fora { continue :fora; };"))
            .OfType<CoreContinue>().ShouldHaveSingleItem().Label.ShouldBe("fora");

    /// <summary>O corpo é uma cadeia de <c>Let</c> comum — o mesmo desugar de qualquer bloco.</summary>
    [Fact]
    public void Loop_BodyDesugarsLikeAnyBlock()
    {
        var loop = AllNodes(Compile("loop { def x = 1; print(x); break; };"))
            .OfType<CoreLoop>().ShouldHaveSingleItem();

        loop.Body.ShouldBeOfType<CoreLet>().Name.ShouldBe("x");
    }

    /// <summary>
    /// <c>if</c> sem chaves imprime com chaves — o <c>CoreSourcePrinter</c> nunca
    /// soube a diferença (plano 26 §26.9) — e o resultado reparseia para a mesma
    /// Core.
    /// </summary>
    [Fact]
    public void If_BareThen_PrintsAndReparsesToTheSameCore()
    {
        const string source = "loop { if true break; else continue; };";

        var original = Print(source);
        var printed = PrintSource(source);
        var reparsed = Print(printed);

        printed.ShouldContain("if true {");
        reparsed.ShouldBe(original, $"código impresso:\n{printed}");
    }

    [Fact]
    public void Loop_RoundTrips() =>
        PrintSource("loop :fora { def i = 1; break :fora, i; };").ShouldContain("loop :fora");

    [Fact]
    public void Break_WithValue_RoundTrips() =>
        PrintSource("loop { break 5; };").ShouldContain("break 5");
}
