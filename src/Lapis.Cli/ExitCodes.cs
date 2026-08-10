using Lapis.Evaluator;

namespace Lapis.Cli;

/// <summary>
/// Códigos de saída do executável <c>lapis</c>.
/// Fixados em plans/01-solution-skeleton.md §1.4 e usados a partir do M1.
/// </summary>
public static class ExitCodes
{
    /// <summary>Execução concluída com sucesso.</summary>
    public const int Success = 0;

    /// <summary>O programa <c>.ls</c> abortou por um erro de execução da linguagem.</summary>
    public const int RuntimeAbort = 1;

    /// <summary>Uso incorreto do CLI (EX_USAGE).</summary>
    public const int Usage = 64;

    /// <summary>Erros de compilação: léxicos, sintáticos ou de tipos.</summary>
    public const int CompilationError = 65;

    /// <summary>Erro interno da implementação (EX_SOFTWARE).</summary>
    public const int InternalError = 70;

    /// <summary>
    /// O código de saída de uma compilação executada até o fim.
    ///
    /// Mora aqui, e não no <c>Program</c>, porque a suíte de conformidade
    /// (plano 11) precisa do <b>mesmo</b> critério: se ela reimplementasse a
    /// decisão, testaria a si mesma em vez de testar o CLI.
    /// </summary>
    public static int For(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.HasErrors)
        {
            return CompilationError;
        }

        return result.Evaluation is { Status: ExecutionStatus.Aborted }
            ? RuntimeAbort
            : Success;
    }
}
