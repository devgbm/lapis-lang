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

    /// <summary>
    /// O parâmetro que dispara a regra do membro de instância (plano 22 §22.1).
    ///
    /// <b>Não é palavra reservada</b>: <c>def self = 1;</c> continua válido, e um
    /// parâmetro chamado <c>self</c> numa função comum é um parâmetro chamado
    /// <c>self</c>. Reservá-la quebraria programa válido sem comprar nada — mesma
    /// decisão de <c>label</c> ser contextual.
    ///
    /// O gatilho é a <b>ausência de anotação</b> na primeira posição de um
    /// <c>def T.m</c>; o nome sozinho não basta.
    /// </summary>
    public const string Self = "self";

    /// <param name="owner">
    /// O dono <b>como escrito</b>, com os argumentos genéricos quando há
    /// (<c>Result&lt;Int, ?&gt;</c>). O padrão entra no nome porque ele faz parte
    /// da identidade do membro: <c>def Result&lt;Int, ?&gt;.d</c> e
    /// <c>def Result&lt;Bool, ?&gt;.d</c> são duas declarações que convivem
    /// (plano 23 §23.4), e duas precisam de dois nomes na Core.
    /// </param>
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

    /// <summary>
    /// O nome do tipo dono, sem o padrão genérico: o <c>Result</c> de
    /// <c>Result&lt;Int, ?&gt;</c>.
    ///
    /// É o que se procura no escopo — <c>Result&lt;Int, ?&gt;</c> não é um nome
    /// ligado a nada, e o padrão só existe para distinguir declarações.
    /// </summary>
    public static string OwnerName(string owner)
    {
        var open = owner.IndexOf('<', StringComparison.Ordinal);
        return open < 0 ? owner : owner[..open];
    }

    /// <summary>Como o nome deve aparecer numa mensagem: <c>User.hello</c>.</summary>
    public static string ToDisplayString(string name) =>
        Split(name) is { } parts ? $"{parts.Owner}.{parts.Member}" : name;
}
