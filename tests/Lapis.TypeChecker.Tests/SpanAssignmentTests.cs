using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

/// <summary>
/// <c>xs[i] = v</c> (Q36) no checker.
///
/// O passo de índice devolve o tipo do <b>elemento</b>, nu — e não
/// <c>Option&lt;T&gt;</c> como a leitura. A assimetria é o centro da decisão: uma
/// leitura precisa produzir valor e pode não haver um; uma escrita fora dos
/// limites não produz nada, então não há o que envelopar.
/// </summary>
public sealed class SpanAssignmentTests : TypeCheckerTestBase
{
    [Fact]
    public void Write_ToElement_Passes() => ShouldPass("var xs = .[1, 2, 3];\nxs[0] = 9;");

    /// <summary>O valor precisa ser o tipo do elemento, não <c>Option</c> dele.</summary>
    [Fact]
    public void Write_ChecksTheElementType() =>
        ShouldFailWith("var xs = .[1, 2, 3];\nxs[0] = \"a\";", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void Write_IndexMustBeInt() =>
        ShouldFailWith("var xs = .[1, 2, 3];\nxs[\"a\"] = 9;", DiagnosticCodes.IndexMustBeInt);

    [Fact]
    public void Write_TargetMustBeIndexable() =>
        ShouldFailWith("var x = 1;\nx[0] = 9;", DiagnosticCodes.NotIndexable);

    /// <summary>Um <c>def</c> continua recusando, índice ou não (Q25).</summary>
    [Fact]
    public void Write_ToDef_IsRejected() =>
        ShouldFailWith("def xs = .[1, 2, 3];\nxs[0] = 9;", DiagnosticCodes.NotAssignable);

    [Fact]
    public void Write_ThroughAFieldPath_Passes() =>
        ShouldPass(
            """
            def Caixa = type {
                xs: [Int;?];
            };

            var c = .Caixa { xs: .[1, 2, 3] };
            c.xs[0] = 9;
            """);

    /// <summary>Um índice fora do escopo é diagnóstico do índice, não do caminho.</summary>
    [Fact]
    public void Write_IndexIsCheckedAsAnExpression() =>
        ShouldFailWith("var xs = .[1, 2, 3];\nxs[naoExiste] = 9;", DiagnosticCodes.UnknownVariable);

    // ------------------------------------------- LAP0246: warning, não erro

    /// <summary>
    /// Com tamanho no tipo e índice constante, o compilador vê que a escrita se
    /// perde — e <b>avisa</b>. Não é erro: a escrita sem efeito deixa o programa
    /// bem definido, e ele roda.
    /// </summary>
    [Fact]
    public void Write_OutOfBounds_WithKnownSize_Warns()
    {
        var (_, diagnostics) = CheckWithDiagnostics("var xs: [Int;3] = .[1, 2, 3];\nxs[5] = 9;");

        diagnostics.ShouldContain(d =>
            d.Code == DiagnosticCodes.SpanWriteOutOfBounds && d.Severity == DiagnosticSeverity.Warning);

        diagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Write_NegativeIndex_WithKnownSize_Warns()
    {
        var (_, diagnostics) = CheckWithDiagnostics("var xs: [Int;3] = .[1, 2, 3];\nxs[-1] = 9;");

        diagnostics.ShouldContain(d => d.Code == DiagnosticCodes.SpanWriteOutOfBounds);
    }

    /// <summary>
    /// "Constante" aqui é o que a Q18 já definia: literal, ou <c>def</c> ligado a
    /// literal. Uma aritmética como <c>0 - 1</c> não é dobrada por este checker,
    /// então não há aviso — provar isso é trabalho do partial evaluator, e é
    /// exatamente o que o M23 traz.
    /// </summary>
    [Fact]
    public void Write_ComputedOutOfBoundsIndex_IsSilent()
    {
        var (_, diagnostics) = CheckWithDiagnostics("var xs: [Int;3] = .[1, 2, 3];\nxs[0 - 1] = 9;");

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Sem o tamanho no tipo não há o que avisar — e é a mesma fronteira em que
    /// a leitura devolve <c>Option</c> em vez de garantir. Um <c>var</c> sem
    /// anotação alarga (Q29), então este é o caso comum.
    /// </summary>
    [Fact]
    public void Write_OutOfBounds_WithUnknownSize_IsSilent()
    {
        var (_, diagnostics) = CheckWithDiagnostics("var xs = .[1, 2, 3];\nxs[99] = 9;");

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Write_InBounds_WithKnownSize_IsSilent()
    {
        var (_, diagnostics) = CheckWithDiagnostics("var xs: [Int;3] = .[1, 2, 3];\nxs[2] = 9;");

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// A <b>leitura</b> no mesmo lugar continua sendo erro (<c>LAP0244</c>), e a
    /// diferença não é descuido: sem elemento não há valor a devolver.
    /// </summary>
    [Fact]
    public void Read_OutOfBounds_IsStillAnError() =>
        ShouldFailWith("def xs = .[1, 2, 3];\ndef x = xs[5];", DiagnosticCodes.IndexOutOfBounds);
}
