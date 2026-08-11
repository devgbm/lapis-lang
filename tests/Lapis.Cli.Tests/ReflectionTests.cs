using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.Cli.Tests;

/// <summary>
/// Metadados do programa como valores comuns (plano 19, spec de macros §11).
/// </summary>
public sealed class ReflectionTests : ConstraintTestBase
{
    private const string Types = """
        def User = type {
            id: Int;
            name: Str;
        };

        def Color = enum {
            Red,
            Green,
            Blue
        };

        """;

    // ------------------------------------------------------ metadados

    [Fact]
    public void Reflect_Struct_Name() => Output(Types + "print(reflect(User).name);").ShouldBe("User\n");

    [Fact]
    public void Reflect_Struct_Fields() =>
        Output(Types + "print(reflect(User).fields);")
            .ShouldBe("[FieldInfo { name: \"id\", typeName: \"Int\" }, "
                + "FieldInfo { name: \"name\", typeName: \"Str\" }]\n");

    [Fact]
    public void Reflect_Struct_HasNoVariants() =>
        Output(Types + "print(reflect(User).variants);").ShouldBe("[]\n");

    [Fact]
    public void Reflect_Enum_Variants() =>
        Output(Types + """
            def nomes = fn(info: TypeInfo) Str {
                match info.variants[0] {
                    Result.Ok(v) => return v.name,
                    Result.Err(e) => return "?"
                }
            };

            print(nomes(reflect(Color)));
            """).ShouldBe("Red\n");

    [Fact]
    public void Reflect_Enum_HasNoFields() =>
        Output(Types + "print(reflect(Color).fields);").ShouldBe("[]\n");

    [Fact]
    public void Reflect_Enum_VariantArity() =>
        Output("print(reflect(Result).variants);")
            .ShouldContain("arity: 1");

    [Fact]
    public void Reflect_Enum_PayloadTypes() =>
        Output("print(reflect(Result).variants);").ShouldContain("payloadTypeNames: [\"T\"]");

    [Fact]
    public void Reflect_Generic_TypeParameterNames() =>
        Output("print(reflect(Result).typeParameterNames);").ShouldBe("[\"T\", \"E\"]\n");

    /// <summary>
    /// `reflect(Box)` descreve a **declaração** — o campo tem tipo `T`;
    /// `reflect(Box&lt;Int&gt;)` descreve a instância. As duas leituras são
    /// legítimas, e ter as duas é o que torna reflection útil sobre genéricos.
    /// </summary>
    [Fact]
    public void Reflect_GenericInstance_SubstitutesArguments()
    {
        const string Box = "def Box = type<T> { value: T; };\n";

        Output(Box + "print(reflect(Box).fields);").ShouldContain("typeName: \"T\"");
        Output(Box + "print(reflect(Box<Int>).fields);").ShouldContain("typeName: \"Int\"");
    }

    [Fact]
    public void Reflect_Kind()
    {
        Output(Types + "print(reflect(User).kind);").ShouldBe("TypeKind.Struct\n");
        Output(Types + "print(reflect(Color).kind);").ShouldBe("TypeKind.Enum\n");
    }

    // --------------------------------------------------------- checagem

    [Fact]
    public void Reflect_NonType_IsError() =>
        Codes("print(reflect(42));").ShouldBe([DiagnosticCodes.ReflectExpectsType]);

    [Fact]
    public void Reflect_Variable_IsError() =>
        Codes("def x = 1;\nprint(reflect(x));").ShouldBe([DiagnosticCodes.ReflectExpectsType]);

    [Fact]
    public void Reflect_WrongArity_IsError() =>
        Codes(Types + "print(reflect(User, Color));").ShouldContain(DiagnosticCodes.ArgumentCountMismatch);

    /// <summary>
    /// Indexar um array de metadados devolve `Result` como qualquer outro array
    /// (spec §21): reflection não escapa das regras da linguagem.
    /// </summary>
    [Fact]
    public void Reflect_FieldAccess_IsAnOrdinaryResult() =>
        Output(Types + "print(reflect(User).fields[0]);")
            .ShouldBe("Result.Ok(FieldInfo { name: \"id\", typeName: \"Int\" })\n");

    /// <summary>
    /// <c>reflect</c> é intrínseco, não binding — mas um binding do usuário com
    /// esse nome <b>vence</b>. Sombrear é permitido em toda parte, e quem escreve
    /// a própria função <c>reflect</c> quis a sua.
    /// </summary>
    [Fact]
    public void Reflect_ShadowedByTheUser_LosesToTheBinding() =>
        Output("def reflect = fn(n: Int) Int { return n * 2; };\nprint(reflect(21));")
            .ShouldBe("42\n");

    // --------------------------------------------------------- runtime

    [Fact]
    public void Reflect_AtRuntime_Prints() =>
        Output(Types + "print(reflect(Color).name);").ShouldBe("Color\n");

    /// <summary>Igualdade estrutural, herdada de struct: nada de especial aqui.</summary>
    [Fact]
    public void Reflect_TwoCalls_AreEqual() =>
        Output(Types + "print(reflect(User) == reflect(User));").ShouldBe("true\n");

    [Fact]
    public void Reflect_DistinctTypes_AreNotEqual() =>
        Output(Types + "print(reflect(User) == reflect(Color));").ShouldBe("false\n");

    // ----------------------------------------------------- compile time

    [Fact]
    public void Reflect_InConstraint_SeesDeclarations() =>
        CompileTimeOutput(Types + """
            macro descreve
                match Str:s
                constraint { print(reflect(Color).name); }
                expand { print(s); };

            @descreve "oi";
            """).ShouldBe("Color\n");

    /// <summary>
    /// A fidelidade em compile time é <b>sintática</b>: o checker ainda não rodou
    /// sobre o programa, então <c>typeName</c> é o tipo como escrito. Aqui isso
    /// aparece como o parâmetro <c>T</c> sobrevivendo onde o runtime já teria
    /// resolvido.
    /// </summary>
    [Fact]
    public void Reflect_InConstraint_IsSyntactic() =>
        CompileTimeOutput("""
            def Box = type<T> {
                value: T;
            };

            macro descreve
                match Str:s
                constraint { print(reflect(Box).fields); }
                expand { print(s); };

            @descreve "oi";
            """).ShouldContain("typeName: \"T\"");

    [Fact]
    public void Reflect_UndeclaredType_InConstraint() =>
        Codes("""
            macro descreve
                match Str:s
                constraint { print(reflect(Inexistente).name); }
                expand { print(s); };

            @descreve "oi";
            """).ShouldContain(DiagnosticCodes.TypeNotDeclaredYet);

    /// <summary>Ordem de declaração respeitada: a mesma regra de `def` (Q8).</summary>
    [Fact]
    public void Reflect_DeclaredLater_IsNotVisible() =>
        Codes("""
            macro descreve
                match Str:s
                constraint { print(reflect(Depois).name); }
                expand { print(s); };

            @descreve "oi";

            def Depois = type {
                x: Int;
            };
            """).ShouldContain(DiagnosticCodes.TypeNotDeclaredYet);

    /// <summary>
    /// Uma captura de <c>Identifier</c> que nomeia um tipo declarado vale como
    /// esse tipo. É o que permite escrever a macro que valida <b>o tipo que
    /// recebeu</b> — sem isso, reflection em compile time seria curiosidade.
    /// </summary>
    [Fact]
    public void Reflect_CapturedIdentifier_ResolvesToTheType() =>
        CompileTimeOutput(Types + """
            macro descreve
                match Identifier:nome
                constraint { print(reflect(nome).name); }
                expand { print(1); };

            @descreve Color;
            """).ShouldBe("Color\n");

    /// <summary>Tipos do prelude continuam pelo caminho normal: eles já estão resolvidos.</summary>
    [Fact]
    public void Reflect_PreludeType_InConstraint() =>
        CompileTimeOutput("""
            macro descreve
                match Str:s
                constraint { print(reflect(Result).typeParameterNames); }
                expand { print(s); };

            @descreve "oi";
            """).ShouldBe("[\"T\", \"E\"]\n");

    /// <summary>
    /// E o programa continua não podendo <b>usar</b> o tipo dentro da constraint:
    /// refletir sobre `Color` é permitido, construir um valor dele não.
    /// </summary>
    [Fact]
    public void Constraint_CannotUseTheTypeItReflects() =>
        Codes(Types + """
            macro descreve
                match Str:s
                constraint { def c = Color.Red; }
                expand { print(s); };

            @descreve "oi";
            """).ShouldContain(DiagnosticCodes.UnknownVariable);
}

/// <summary>
/// Reflection e o partial evaluator (plano 19 §19.5) — o caso limpo em que uma
/// feature aparentemente cara sai de graça depois da especialização.
/// </summary>
public sealed class ReflectionSpecializationTests
{
    private static string Specialize(string source)
    {
        var compiled = Pipeline.Compile(SourceText.From(source), PipelineStage.TypeCheck);

        compiled.HasErrors.ShouldBeFalse(
            string.Join(", ", compiled.Diagnostics.Select(d => $"{d.Code} {d.Message}")));

        var residual = PartialEvaluator.PartialEvaluator.Specialize(
            compiled.Core!, types: compiled.Typed, prelude: PreludeLoader.Load());

        return Ast.Printing.CoreSourcePrinter.Print(residual.Residual);
    }

    [Fact]
    public void Reflect_IsFoldedByPE() =>
        Specialize("def User = type { id: Int; };\nprint(reflect(User).name);")
            .ShouldContain("print(\"User\")");

    [Fact]
    public void Reflect_TypeParameterNames_AreFolded() =>
        Specialize("print(reflect(Result).typeParameterNames);")
            .ShouldContain("""print(["T", "E"])""");

    /// <summary>
    /// O <c>TypeInfo</c> inteiro <b>não</b> vira literal: a expressão que
    /// reconstrói um struct cita o nome do tipo, e esse nome pode estar sombreado
    /// no ponto de emissão (decisão do M7). O PE deixa de progredir, nunca de
    /// estar correto — <c>reflect(User)</c> sobrevive intacto no residual.
    /// </summary>
    [Fact]
    public void Reflect_WholeStruct_IsNotResidualized() =>
        Specialize("def User = type { id: Int; };\nprint(reflect(User));")
            .ShouldContain("reflect(User)");
}
