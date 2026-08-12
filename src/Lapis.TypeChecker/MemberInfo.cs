using Lapis.Ast.Typed;
using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker;

/// <summary>
/// Um membro declarado por <c>def T.m = e;</c> (plano 21 §21.5).
///
/// <paramref name="SyntheticName"/> é o nome com que o valor vive na Core
/// (<c>User@hello</c>): o desugar já emitiu o <c>Let</c>, então o evaluator não
/// precisa de caminho especial — basta ler o nome.
///
/// <paramref name="Kind"/> separa valor de método estático, e vai separar método
/// de instância no plano 22. A separação existe porque <c>User.hello</c> e
/// <c>user.hello()</c> não podem ser dois caminhos para a mesma coisa.
/// </summary>
public sealed record MemberInfo(
    string Name,
    MemberAccessKind Kind,
    LapisType Type,
    string SyntheticName,
    SourceSpan Span);
