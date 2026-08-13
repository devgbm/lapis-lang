using Lapis.Ast;
using Lapis.Ast.Core;
using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.PartialEvaluator;

/// <summary>
/// Converte um valor conhecido de volta em expressão — a "lift"/"reification"
/// clássica.
///
/// É o único lugar onde o PE pode deixar de progredir, nunca de estar correto:
/// quando um valor não tem forma sintática, o PE mantém a expressão original em
/// vez de inventar uma.
/// </summary>
public sealed class Residualizer(CoreFactory factory)
{
    /// <summary>
    /// Este valor volta a ser expressão sem depender de nenhum nome em escopo?
    ///
    /// Primitivos e arrays deles, sim — um literal não referencia nada. Uma
    /// closure, um enum construído (<c>Result.Ok(20)</c>) ou um struct, não: a
    /// expressão que os reconstrói cita <c>Result</c> ou o nome do tipo, e esse
    /// nome pode estar sombreado no ponto onde o residual seria emitido. Um PE que
    /// captura nome deixa de preservar o comportamento (spec §40), e preservar o
    /// comportamento é a única coisa que ele não pode negociar.
    ///
    /// A consequência prática é conhecida e aceita: <c>[10,20,30][1]</c> não dobra
    /// para <c>Result.Ok(20)</c> neste plano. Isso é trabalho do plano 14, junto
    /// com a eliminação de bounds check, que é onde a construção do <c>Result</c>
    /// deixa de ser um detalhe e passa a ser o ponto.
    /// </summary>
    public static bool CanResidualize(Value value) => value switch
    {
        IntValue or FloatValue or BoolValue or StrValue or CharValue or VoidValue => true,

        // Um array **vazio** não diz o que carrega: `[]` sozinho não é programa
        // válido (spec §18, LAP0241), e a anotação que o tornava válido some
        // quando o valor substitui o `def`. Emiti-lo produziria um residual que
        // não compila — o PE tem que manter a expressão original.
        SpanValue array => !array.Elements.IsEmpty && array.Elements.All(CanResidualize),

        _ => false,
    };

    public CoreExpr Residualize(Value value, SourceSpan span) => value switch
    {
        IntValue v => factory.Literal(span, new ConstInt(v.Value)),
        FloatValue v => factory.Literal(span, new ConstFloat(v.Value)),
        BoolValue v => factory.Literal(span, v.Value ? ConstBool.True : ConstBool.False),
        StrValue v => factory.Literal(span, new ConstStr(v.Value)),
        CharValue v => factory.Literal(span, new ConstChar(v.Value)),
        VoidValue => factory.Unit(span),

        SpanValue v => factory.Array(span, [.. v.Elements.Select(e => Residualize(e, span))]),

        _ => throw new InternalCompilerException(
            $"valor sem forma sintática chegou ao residualizador: {value.GetType().Name}", span),
    };

    /// <summary>A expressão que representa este resultado no programa residual.</summary>
    public CoreExpr Residualize(PEResult result, SourceSpan span) => result switch
    {
        StaticResult s => Residualize(s.Value, span),
        DynamicResult d => d.Residual,
        _ => throw new InternalCompilerException("resultado de PE desconhecido", span),
    };
}
