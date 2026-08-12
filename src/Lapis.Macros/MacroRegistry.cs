using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Macros;

/// <summary>
/// As macros visíveis num arquivo.
///
/// Macros são visíveis <b>do ponto da declaração em diante</b> — a mesma regra de
/// <c>def</c> (Q8) — e ocupam um espaço de nomes próprio: <c>macro log</c> e
/// <c>def log = fn ...</c> convivem, porque <c>@log</c> e <c>log</c> nunca se
/// confundem.
///
/// Duas camadas, pela mesma razão que o escopo de valores tem duas: as do
/// <c>prelude.ls</c> (plano 20) e as do arquivo. Redeclarar uma do prelude
/// <b>sombreia</b>, sem diagnóstico — quem escreve <c>macro while</c> no seu
/// arquivo quis o seu; redeclarar uma do próprio arquivo continua sendo
/// <c>LAP0511</c>.
/// </summary>
public sealed class MacroRegistry
{
    private readonly Dictionary<string, MacroDeclaration> _prelude = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MacroDeclaration> _macros = new(StringComparer.Ordinal);

    /// <summary>
    /// A camada de baixo. Não passa por <see cref="TryRegister"/> porque não é
    /// declaração do arquivo: se o prelude tivesse duas macros com o mesmo nome
    /// seria bug do compilador, não erro do usuário, e a carga do prelude já
    /// rejeita isso.
    /// </summary>
    public void RegisterPrelude(IEnumerable<MacroDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);

        foreach (var declaration in declarations)
        {
            _prelude[declaration.Name] = declaration;
        }
    }

    public bool TryRegister(MacroDeclaration declaration, DiagnosticBag diagnostics)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (_macros.TryGetValue(declaration.Name, out var previous))
        {
            diagnostics.ReportError(
                DiagnosticCodes.DuplicateMacro,
                declaration.NameSpan,
                $"a macro '{declaration.Name}' já foi declarada",
                new DiagnosticNote("declaração anterior", previous.NameSpan));

            return false;
        }

        _macros[declaration.Name] = declaration;
        return true;
    }

    public bool TryLookup(string name, out MacroDeclaration declaration) =>
        _macros.TryGetValue(name, out declaration!) || _prelude.TryGetValue(name, out declaration!);

    public bool IsEmpty => _macros.Count == 0 && _prelude.Count == 0;
}
