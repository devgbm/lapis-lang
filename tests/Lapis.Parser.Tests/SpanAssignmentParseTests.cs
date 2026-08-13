using Lapis.Ast.Surface;

namespace Lapis.Parser.Tests;

/// <summary>
/// <c>xs[i] = v;</c> (Q36) — o caminho de atribuição, que só tinha campos,
/// passa a ter índices.
///
/// O reconhecimento é o ponto delicado: `AtAssignment` decide se um statement é
/// atribuição olhando adiante sem consumir, e um índice é expressão de tamanho
/// arbitrário. A busca é por casamento de colchetes, e não por contagem fixa de
/// tokens.
/// </summary>
public sealed class SpanAssignmentParseTests : ParserTestBase
{
    [Fact]
    public void Assign_ToElement() =>
        ShouldPrintStatementAs("xs[0] = 1;", "(assign xs[] (int 0) (int 1))");

    /// <summary>O índice é expressão qualquer — é o que torna `map` escrevível.</summary>
    [Fact]
    public void Assign_ToComputedIndex() =>
        ShouldPrintStatementAs("xs[i + 1] = 1;", "(assign xs[] (binary + (name i) (int 1)) (int 1))");

    [Fact]
    public void Assign_MixesFieldsAndIndices() =>
        ShouldPrintStatementAs("c.xs[0].a = 1;", "(assign c.xs[].a (int 0) (int 1))");

    [Fact]
    public void Assign_NestedIndices() =>
        ShouldPrintStatementAs("m[0][1] = 1;", "(assign m[][] (int 0) (int 1) (int 1))");

    [Fact]
    public void Assign_KeepsTheSegmentsInOrder()
    {
        var assign = Parse("c.xs[7] = 1;")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<AssignStatement>();

        assign.Name.ShouldBe("c");
        assign.Path.Length.ShouldBe(2);
        assign.Path[0].ShouldBeOfType<FieldSegment>().Name.ShouldBe("xs");
        assign.Path[1].ShouldBeOfType<IndexSegment>().Index.ShouldBeOfType<IntLiteral>().Value.ShouldBe(7);
    }

    /// <summary>
    /// Uma indexação que <b>não</b> é seguida de <c>=</c> continua sendo
    /// expressão. `==` é outro token, então não há ambiguidade a resolver — mas
    /// vale travar, porque é o caso que uma busca de colchetes desatenta
    /// quebraria.
    /// </summary>
    [Fact]
    public void Index_WithoutEquals_IsStillAnExpression() =>
        ShouldPrintStatementAs(
            "print(xs[0] == ys[0]);",
            "(stmt (call (name print) (binary == (index (name xs) (int 0)) (index (name ys) (int 0)))))");

    /// <summary>
    /// Um `[` sem par não faz o parser varrer o arquivo à procura do fecho: a
    /// busca para na fronteira do statement, e o que sai é o erro de sintaxe
    /// local em vez de um arquivo inteiro reinterpretado.
    /// </summary>
    [Fact]
    public void UnclosedBracket_DoesNotSwallowTheFile() =>
        Codes("xs[0 = 1;\nprint(1);").ShouldNotBeEmpty();
}
