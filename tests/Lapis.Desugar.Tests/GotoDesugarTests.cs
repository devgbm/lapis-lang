using Lapis.Ast.Core;

namespace Lapis.Desugar.Tests;

/// <summary>Decomposição em blocos básicos (plano 16 §16.4).</summary>
public sealed class GotoDesugarTests : DesugarTestBase
{
    /// <summary>Sem rótulo, nada muda: um bloco comum continua uma cadeia de <c>Let</c>.</summary>
    [Fact]
    public void Block_WithoutLabels_HasNoLabeled() =>
        AllNodes(Compile("def x = 1;\nprint(x);")).ShouldNotContain(n => n is CoreLabeled);

    /// <summary>Um <c>goto</c> sem rótulo local também não abre grupo: ele sobe.</summary>
    [Fact]
    public void GotoWithoutLocalLabel_HasNoLabeled()
    {
        var nodes = AllNodes(Compile("label fora;\ndef f = fn() Void { goto fora; };"));

        nodes.OfType<CoreLabeled>().Count().ShouldBe(1);
    }

    [Fact]
    public void Label_SplitsIntoOneJoin()
    {
        var labeled = SingleLabeled("goto fim;\nprint(1);\nlabel fim;\nprint(2);");

        labeled.Joins.ShouldHaveSingleItem().Name.ShouldBe("fim");
    }

    [Fact]
    public void MultipleLabels_ProduceMultipleJoins()
    {
        var labeled = SingleLabeled("goto c;\nlabel a;\nlabel b;\nlabel c;\nprint(1);");

        labeled.Joins.Select(j => j.Name).ShouldBe(["a", "b", "c"]);
    }

    /// <summary>
    /// Um <c>label</c> encerra o segmento anterior com um salto implícito: é o que
    /// torna a decomposição um sufixo, e não uma cópia do que vem depois.
    /// </summary>
    [Fact]
    public void Label_TerminatesSegmentWithImplicitGoto()
    {
        var labeled = SingleLabeled("print(1);\nlabel fim;\nprint(2);");

        var terminator = Tail(labeled.Entry).ShouldBeOfType<CoreGoto>();

        terminator.Label.ShouldBe("fim");
        terminator.IsImplicit.ShouldBeTrue();
    }

    /// <summary>
    /// O grupo abre no primeiro salto: o que veio antes continua envolvendo-o, e
    /// portanto continua em escopo nos dois lados (regra 2).
    /// </summary>
    [Fact]
    public void Labeled_OpensAtFirstJump()
    {
        var outer = Compile("def x = 1;\ngoto fim;\nlabel fim;\nprint(x);")
            .Body.ShouldBeOfType<CoreLet>();

        outer.Name.ShouldBe("x");
        outer.Body.ShouldBeOfType<CoreLabeled>();
    }

    /// <summary>
    /// E o que é declarado entre o salto e o rótulo fica dentro da entrada — não
    /// é limitação, é a verdade: o salto pode ter pulado a declaração (regra 3).
    /// </summary>
    [Fact]
    public void DefinitionBetweenJumpAndLabel_StaysInsideTheEntry()
    {
        var labeled = SingleLabeled("goto fim;\ndef y = 2;\nlabel fim;\nprint(1);");

        Lets(labeled.Entry).ShouldContain("y");
        Lets(labeled.Joins[0].Body).ShouldNotContain("y");
    }

    [Fact]
    public void GotoIf_IsPrimitive_NotDesugaredToIf()
    {
        var nodes = AllNodes(Compile("goto fim if true;\nlabel fim;"));

        nodes.ShouldContain(n => n is CoreGotoIf);
        nodes.ShouldNotContain(n => n is CoreIf);
    }

    /// <summary>Um salto escrito à mão não é implícito — e por isso é impresso.</summary>
    [Fact]
    public void ExplicitGoto_IsNotMarkedImplicit()
    {
        var explicitJump = AllNodes(Compile("goto fim;\nlabel fim;"))
            .OfType<CoreGoto>()
            .First(g => !g.IsImplicit);

        explicitJump.Label.ShouldBe("fim");
    }

    // ------------------------------------------------------------ helpers

    private static CoreLabeled SingleLabeled(string source) =>
        AllNodes(Compile(source)).OfType<CoreLabeled>().ShouldHaveSingleItem();

    /// <summary>O que sobra depois da cadeia de <c>Let</c>.</summary>
    private static CoreExpr Tail(CoreExpr node)
    {
        var current = node;

        while (current is CoreLet let)
        {
            current = let.Body;
        }

        return current;
    }

    private static List<string> Lets(CoreExpr node)
    {
        var names = new List<string>();

        for (var current = node; current is CoreLet let; current = let.Body)
        {
            names.Add(let.Name);
        }

        return names;
    }
}
