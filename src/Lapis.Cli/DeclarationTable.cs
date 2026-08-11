using System.Collections.Immutable;
using Lapis.Ast.Printing;
using Lapis.Ast.Surface;
using Lapis.Ast.Types;
using Lapis.Runtime;

namespace Lapis.Cli;

/// <summary>
/// Os <c>type</c> e <c>enum</c> que o programa declara, lidos <b>da sintaxe</b>
/// (plano 19 §19.3).
///
/// É a fonte de metadados do compile time, e existe porque durante a expansão de
/// macros o type checker ainda não rodou: não há <c>TypeDefinition</c> nenhuma de
/// onde tirar campos e variantes. A diferença de fidelidade em relação ao runtime
/// é inevitável e vale dizer em voz alta — dentro de um <c>constraint</c>,
/// <c>reflect(Box).fields[0].typeName</c> é <c>"T"</c>, o tipo <i>como escrito</i>;
/// em runtime, depois de <c>Box&lt;Int&gt;</c>, é <c>"Int"</c>.
///
/// Para o que uma constraint precisa — nomes de campos, de variantes e aridade — a
/// informação sintática basta.
///
/// Mora no orquestrador pelo mesmo motivo que <see cref="PreludeLoader"/>: ele é
/// quem enxerga a Surface AST inteira antes de qualquer fase posterior.
/// </summary>
public sealed class DeclarationTable
{
    private readonly ImmutableArray<Declared> _declarations;

    private DeclarationTable(ImmutableArray<Declared> declarations) => _declarations = declarations;

    /// <summary>
    /// Varre os <c>def</c> de topo cujo valor é um <c>type</c> ou um <c>enum</c>.
    ///
    /// Só o topo: um tipo declarado dentro de uma função é local a ela, e uma
    /// macro que o enxergasse estaria lendo um escopo que ainda nem existe.
    /// </summary>
    public static DeclarationTable Of(SourceFile file, PreludeScope prelude)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(prelude);

        var declarations = ImmutableArray.CreateBuilder<Declared>();

        foreach (var statement in file.Statements)
        {
            if (statement is DefStatement { Value: TypeExpression or EnumExpression } declaration)
            {
                declarations.Add(new Declared(
                    declaration.Name, declaration.Span.Start, Describe(declaration, prelude)));
            }
        }

        return new DeclarationTable(declarations.ToImmutable());
    }

    /// <summary>
    /// Os tipos visíveis num ponto do arquivo — os declarados <b>acima</b> dele.
    ///
    /// A regra é a de <c>def</c> (Q8): um nome vale do ponto da declaração em
    /// diante. Uma macro que enxergasse um tipo declarado depois dela veria o
    /// arquivo de um jeito que nenhuma outra fase vê.
    /// </summary>
    public IReadOnlyDictionary<string, StructValue> VisibleAt(int offset)
    {
        var visible = ImmutableDictionary.CreateBuilder<string, StructValue>(StringComparer.Ordinal);

        foreach (var declaration in _declarations)
        {
            if (declaration.Start < offset)
            {
                visible[declaration.Name] = declaration.Info;
            }
        }

        return visible.ToImmutable();
    }

    private static StructValue Describe(DefStatement declaration, PreludeScope prelude) =>
        declaration.Value switch
        {
            TypeExpression type => prelude.MakeTypeInfo(
                declaration.Name,
                TypeDefinitionKind.Struct,
                [.. type.TypeParameters.Select(p => p.Name)],
                [.. type.Fields.Select(f => (f.Name, SurfaceSExprPrinter.PrintType(f.Type)))],
                []),

            EnumExpression @enum => prelude.MakeTypeInfo(
                declaration.Name,
                TypeDefinitionKind.Enum,
                [.. @enum.TypeParameters.Select(p => p.Name)],
                [],
                [.. @enum.Variants.Select(v => (
                    v.Name,
                    (IReadOnlyList<string>)[.. v.Payload.Select(SurfaceSExprPrinter.PrintType)]))]),

            _ => throw new Diagnostics.InternalCompilerException(
                $"'{declaration.Name}' não é um type nem um enum", declaration.Span),
        };

    private readonly record struct Declared(string Name, int Start, StructValue Info);
}
