using Lapis.Ast.Core;

namespace Lapis.PartialEvaluator;

/// <summary>
/// Classificação de efeitos — a diferença entre um partial evaluator correto e um
/// que "otimiza" quebrando o programa (plano 12 §12.4).
///
/// Conservadora por construção: na dúvida, impura. Uma expressão classificada como
/// pura por engano permite ao PE duplicá-la ou eliminá-la, e aí o residual deixa
/// de ser equivalente — que é a única falha que o PE não pode ter (spec §40).
/// </summary>
public static class Effects
{
    /// <summary>
    /// Avaliar esta expressão é observável? "Observável" aqui é: escreve na saída,
    /// ou faz qualquer coisa que o programa consiga notar mais de uma vez.
    /// </summary>
    public static bool IsPure(CoreExpr expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            CoreLiteral or CoreVariable or CoreEnumDef or CoreTypeDef => true,

            // Criar uma closure não tem efeito; chamá-la tem. O corpo não é
            // examinado aqui justamente por isso.
            CoreLambda => true,

            CoreLet n => IsPure(n.Value) && IsPure(n.Body),

            // Toda aritmética é pura e dobrável sem análise nenhuma: com a Q9 a
            // divisão inteira por zero produz o maior `Int` em vez de abortar,
            // então não existe operador aritmético parcial. É um ganho direto
            // daquela decisão.
            CoreBinary n => IsPure(n.Left) && IsPure(n.Right),
            CoreUnary n => IsPure(n.Operand),

            CoreIf n => IsPure(n.Condition) && IsPure(n.Then) && IsPure(n.Else),

            CoreSpan n => n.Elements.All(IsPure),
            CoreSpanRepeat n => IsPure(n.Initializer) && IsPure(n.Size),
            CoreIndex n => IsPure(n.Target) && IsPure(n.Index),
            CoreField n => IsPure(n.Target),
            CoreInstantiate n => IsPure(n.Target),

            CoreConstruct n => n.Fields.All(f => IsPure(f.Value)),

            CoreMatch n => IsPure(n.Scrutinee) && n.Arms.All(a => IsPure(a.Body)),

            // `is` é ramificação, como `If` e `Match`: puro quando o escrutinado
            // e os dois ramos são (plano 25).
            CoreIs n => IsPure(n.Scrutinee) && IsPure(n.Then) && IsPure(n.Else),

            // Chamada é impura por conservadorismo: o PE não sabe o que há do
            // outro lado, e `print` é justamente uma chamada.
            CoreCall => false,

            // `return`, `break` e `continue` são fluxo de controle: mover ou
            // duplicar um deles muda para onde o programa vai, que é observável.
            // Um `loop` não é puro pelo mesmo motivo — mover ou duplicar quantas
            // vezes ele itera é observável — mesmo quando o corpo, isolado, seria.
            CoreReturn or CoreLoop or CoreBreak or CoreContinue => false,

            // `throw` aborta a compilação: eliminá-lo por ser "sem efeito" seria
            // apagar exatamente o efeito que ele tem.
            CoreThrow => false,

            // Atribuição é o efeito por excelência.
            CoreAssign => false,

            _ => false,
        };
    }
}
