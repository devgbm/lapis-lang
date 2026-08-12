namespace Lapis.Ast;

/// <summary>
/// O nome com que um membro de tipo vive na Core (plano 21 §21.4).
///
/// <c>def User.hello = ...</c> vira um <c>Let</c> comum ligado a
/// <c>User@hello</c>. O desugar só <b>nomeia</b>; quem liga <c>User.hello</c> ao
/// nome sintético é o checker, que é onde a informação de tipo existe.
///
/// O separador é <c>#</c>: não é lexável em identificador nenhum, então nenhum
/// nome escrito pelo usuário colide com um nome de membro. <c>User_hello</c> — a
/// alternativa por concatenação textual — colidiria com um <c>def User_hello</c>,
/// que é programa válido hoje.
///
/// <b>Não</b> é o <c>@</c> da higiene de macro, embora a técnica seja a mesma. O
/// lexer absorve <c>nome@&lt;dígitos&gt;</c> como um identificador só (é assim que
/// uma marca de expansão sobrevive ao round-trip do printer), então os dois
/// esquemas dividiriam o mesmo espaço de nomes: <c>temp@1</c> seria lido como o
/// membro <c>1</c> do tipo <c>temp</c>. Separadores distintos tornam a
/// sobreposição impossível em vez de improvável.
/// </summary>
public static class MemberNames
{
    public const char Separator = '#';

    public static string Of(string owner, string member) => $"{owner}{Separator}{member}";

    /// <summary>
    /// O par por trás de um nome sintético, ou <c>null</c> se o nome for comum.
    ///
    /// Serve a quem precisa reapresentar o nome ao usuário — um diagnóstico não
    /// deve dizer <c>User@hello</c>, porque isso não é escrevível.
    /// </summary>
    public static (string Owner, string Member)? Split(string name)
    {
        var at = name.IndexOf(Separator, StringComparison.Ordinal);

        return at <= 0 || at == name.Length - 1
            ? null
            : (name[..at], name[(at + 1)..]);
    }

    /// <summary>Como o nome deve aparecer numa mensagem: <c>User.hello</c>.</summary>
    public static string ToDisplayString(string name) =>
        Split(name) is { } parts ? $"{parts.Owner}.{parts.Member}" : name;
}
