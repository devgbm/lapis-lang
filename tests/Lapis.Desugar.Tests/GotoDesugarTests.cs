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

    // ------------------------------------------- particionamento em grupos

    /// <summary>
    /// Dois rótulos sem salto entre eles não são irmãos: o segundo abre um grupo
    /// <b>aninhado</b> na continuação do primeiro.
    ///
    /// É o que faz uma declaração escrita entre os dois estar em escopo no
    /// segundo — sem isso, dois <c>@while</c> no mesmo bloco não conseguem
    /// declarar cada um o seu contador.
    /// </summary>
    [Fact]
    public void IndependentLabels_AreNestedGroups()
    {
        var groups = AllNodes(Compile("label a;\nprint(1);\nlabel b;\nprint(2);"))
            .OfType<CoreLabeled>()
            .ToList();

        groups.Count.ShouldBe(2);
        groups.SelectMany(g => g.Joins).Select(j => j.Name).ShouldBe(["a", "b"], ignoreOrder: true);
        groups.ShouldAllBe(g => g.Joins.Length == 1);
    }

    /// <summary>
    /// Um <c>goto</c> explícito que atravessa a fronteira mantém os dois no mesmo
    /// grupo: o salto pode ter pulado o que foi declarado no caminho, e o destino
    /// não pode enxergar essas declarações.
    /// </summary>
    [Fact]
    public void ForwardJump_KeepsLabelsInTheSameGroup() =>
        SingleLabeled("label a;\ngoto b;\nprint(1);\nlabel b;\nprint(2);")
            .Joins.Select(j => j.Name).ShouldBe(["a", "b"]);

    /// <summary>E o salto conta mesmo escondido dentro de um <c>if</c>.</summary>
    [Fact]
    public void ForwardJump_InsideAnIf_AlsoKeepsTheGroup() =>
        SingleLabeled("label a;\nif true { goto b; }\nlabel b;\nprint(2);")
            .Joins.Select(j => j.Name).ShouldBe(["a", "b"]);

    /// <summary>
    /// O formato que <c>@while</c> gera: <c>top</c> e <c>done</c> continuam
    /// irmãos, porque o corpo salta para <c>done</c> para sair do laço.
    /// </summary>
    [Fact]
    public void WhileShape_KeepsTopAndDoneTogether() =>
        SingleLabeled("""
            label top;
            goto done if true;
            print(1);
            goto top;
            label done;
            print(2);
            """).Joins.Select(j => j.Name).ShouldBe(["top", "done"]);

    /// <summary>
    /// Dois laços seguidos: dois grupos de dois, aninhados — e não um grupo de
    /// quatro irmãos.
    /// </summary>
    [Fact]
    public void TwoLoops_ProduceTwoNestedGroups()
    {
        var groups = AllNodes(Compile("""
            label top1;
            goto done1 if true;
            goto top1;
            label done1;
            label top2;
            goto done2 if true;
            goto top2;
            label done2;
            print(1);
            """)).OfType<CoreLabeled>().ToList();

        groups.Count.ShouldBe(2);
        groups.ShouldAllBe(g => g.Joins.Length == 2);
    }

    /// <summary>
    /// Rótulo repetido fica no mesmo grupo de propósito: é o que mantém
    /// <c>LAP0522</c> sendo reportado, em vez de um virar sombra do outro.
    /// </summary>
    [Fact]
    public void RepeatedName_StaysInTheSameGroup() =>
        SingleLabeled("label x;\nprint(1);\nlabel x;\nprint(2);")
            .Joins.Select(j => j.Name).ShouldBe(["x", "x"]);

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
