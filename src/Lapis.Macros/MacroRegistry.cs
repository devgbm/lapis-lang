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
/// </summary>
public sealed class MacroRegistry
{
    private readonly Dictionary<string, MacroDeclaration> _macros = new(StringComparer.Ordinal);

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
        _macros.TryGetValue(name, out declaration!);

    public bool IsEmpty => _macros.Count == 0;
}
