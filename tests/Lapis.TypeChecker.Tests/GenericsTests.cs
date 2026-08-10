using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

/// <summary>Fase D do plano 06: generics de tipo, sempre explícitos (Q7).</summary>
public sealed class GenericFunctionTests : TypeCheckerTestBase
{
    private const string Identity = "def identity = fn<T>(value: T) T { return value; };\n";

    [Fact]
    public void Generic_Identity_Explicit() =>
        TypeOfDef(Identity + "def r = identity<Int>(10);", "r").ShouldBe(PrimitiveType.Int);

    [Fact]
    public void Generic_Substitution_InReturnType() =>
        TypeOfDef(Identity + """def r = identity<Str>("a");""", "r").ShouldBe(PrimitiveType.Str);

    /// <summary>Sem argumentos genéricos não há de onde inferir: Q7.</summary>
    [Fact]
    public void Generic_Identity_Omitted_IsError() =>
        ShouldFailWith(Identity + "def r = identity(10);", DiagnosticCodes.GenericArityMismatch);

    [Fact]
    public void Generic_Identity_WrongArgument() =>
        ShouldFailWith(Identity + """def r = identity<Int>("s");""", DiagnosticCodes.ArgumentTypeMismatch);

    [Fact]
    public void Generic_ArityMismatch() =>
        ShouldFailWith(Identity + "def r = identity<Int, Str>(1);", DiagnosticCodes.GenericArityMismatch);

    [Fact]
    public void Generic_Substitution_InArrayParameter()
    {
        const string Source = """
            def first = fn<T>(xs: T[], fallback: T) T {
                match xs[0] {
                    Result.Ok(value) => return value,
                    Result.Err(e) => return fallback
                }
            };

            def r = first<Int>([1, 2, 3], 0);
            """;

        TypeOfDef(Source, "r").ShouldBe(PrimitiveType.Int);
    }

    /// <summary>
    /// A substituição desce por dentro dos argumentos de um tipo nomeado: o
    /// parâmetro <c>T</c> em <c>Result&lt;T, IndexError&gt;</c> precisa virar
    /// <c>Int</c>, senão nada casa com o parâmetro instanciado.
    /// </summary>
    [Fact]
    public void Generic_Substitution_InsideNamedTypeArguments()
    {
        const string Source = """
            def unwrapOr = fn<T>(r: Result<T, IndexError>, fallback: T) T {
                match r {
                    Result.Ok(value) => return value,
                    Result.Err(error) => return fallback
                }
            };

            def r = unwrapOr<Int>([10, 20][1], 0);
            """;

        TypeOfDef(Source, "r").ShouldBe(PrimitiveType.Int);
    }

    /// <summary>
    /// Uma assinatura genérica aninhada atravessa a substituição intacta: o
    /// <c>T</c> interno é o dela, não o de fora.
    /// </summary>
    [Fact]
    public void Generic_Substitution_DoesNotEnterNestedGenericSignature() =>
        ShouldPass("""
            def outer = fn<T>(value: T) T {
                def inner = fn<T>(x: T) T {
                    return x;
                };

                return inner<T>(value);
            };

            def r = outer<Int>(1);
            """);

    /// <summary>Um parâmetro de tipo é visível no corpo, e só nele.</summary>
    [Fact]
    public void TypeParameter_DoesNotEscapeTheDeclaration() =>
        ShouldFailWith(Identity + "def x: T = 1;", DiagnosticCodes.UnknownType);

    [Fact]
    public void Instantiating_NonGeneric_IsError() =>
        ShouldFailWith(
            "def f = fn(a: Int) Int { return a; };\ndef r = f<Int>(1);",
            DiagnosticCodes.GenericArityMismatch);

    [Fact]
    public void DuplicateTypeParameter_IsError() =>
        ShouldFailWith(
            "def f = fn<T, T>(v: T) T { return v; };", DiagnosticCodes.DuplicateDefinition);

    /// <summary>O checker instancia por substituição; nada é monomorfizado.</summary>
    [Fact]
    public void GenericSignature_IsGenericBeforeInstantiation()
    {
        var signature = TypeOfDef(Identity, "identity").ShouldBeOfType<FunctionType>();

        signature.IsGeneric.ShouldBeTrue();
        signature.TypeParameters.ShouldHaveSingleItem().Name.ShouldBe("T");
    }
}

public sealed class GenericTypeInstantiationTests : TypeCheckerTestBase
{
    private const string Box = "def Box = type<T> { value: T; };\n";

    [Fact]
    public void Generic_Type_Instantiation()
    {
        var type = TypeOfDef(Box + "def b = .Box<Int> { value: 1 };", "b").ShouldBeOfType<NamedType>();

        type.Definition.Name.ShouldBe("Box");
        type.Arguments.ShouldHaveSingleItem().ShouldBe(new TypeArgument(PrimitiveType.Int));
    }

    [Fact]
    public void Generic_Field_UsesInstanceArguments() =>
        TypeOfDef(Box + "def v = .Box<Str> { value: \"a\" }.value;", "v").ShouldBe(PrimitiveType.Str);

    [Fact]
    public void Generic_Type_WithoutArguments_IsError() =>
        ShouldFailWith(Box + "def b: Box = x;", DiagnosticCodes.GenericTypeNeedsArguments);

    [Fact]
    public void Generic_DistinctInstantiations_AreDistinctTypes()
    {
        const string Source = Box + """
            def a = .Box<Int> { value: 1 };
            def b = .Box<Str> { value: "s" };
            """;

        var types = TypesOfDefs(Source, "a", "b");

        types[0].ShouldNotBe(types[1]);
    }

    [Fact]
    public void Generic_Enum_Result() =>
        ShouldPass("def r: Result<Int, IndexError> = [1][0];");

    /// <summary>
    /// Construir a variante de um enum genérico exige os argumentos: sem inferência
    /// (Q7), <c>Result.Ok(1)</c> não diz qual é o tipo do erro.
    /// </summary>
    [Fact]
    public void GenericVariant_WithoutArguments_IsError() =>
        ShouldFailWith("def r = Result.Ok(1);", DiagnosticCodes.CannotDetermineGenericArguments);

    [Fact]
    public void GenericVariant_WithArguments_Typechecks()
    {
        var type = TypeOfDef("def r = Result<Int, IndexError>.Ok(1);", "r").ShouldBeOfType<NamedType>();

        type.Definition.Name.ShouldBe("Result");
        type.Arguments.Length.ShouldBe(2);
    }

    [Fact]
    public void GenericVariant_PayloadIsSubstituted() =>
        ShouldFailWith(
            """def r = Result<Int, IndexError>.Ok("s");""", DiagnosticCodes.ArgumentTypeMismatch);

    /// <summary>Uma variante nulária de enum genérico também precisa dos argumentos.</summary>
    [Fact]
    public void GenericVariant_Nullary_Typechecks()
    {
        const string Source = """
            def Option = enum<T> { Some(T), None };
            def o = Option<Int>.None;
            """;

        TypeOfDef(Source, "o").ShouldBeOfType<NamedType>().Definition.Name.ShouldBe("Option");
    }

    [Fact]
    public void GenericVariant_MatchesInstantiatedEnum() =>
        ShouldPass("""
            def Option = enum<T> { Some(T), None };

            def unwrap = fn(o: Option<Int>, fallback: Int) Int {
                match o {
                    Option.Some(v) => return v,
                    Option.None => return fallback
                }
            };
            """);
}

/// <summary>Const generics — spec §13, Q1.</summary>
public sealed class ConstGenericTests : TypeCheckerTestBase
{
    private const string FixedArray = "def FixedArray = type<T, N: Int> { values: T[]; };\n";

    [Fact]
    public void Const_Generic_Ok() =>
        ShouldPass(FixedArray + "def a: FixedArray<Int, 3> = .FixedArray<Int, 3> { values: [1, 2, 3] };");

    [Fact]
    public void Const_Generic_WrongKind_TypeForConst() =>
        ShouldFailWith(FixedArray + "def a: FixedArray<Int, Str> = x;", DiagnosticCodes.ExpectedConstArgument);

    [Fact]
    public void Const_Generic_WrongKind_ConstForType() =>
        ShouldFailWith(FixedArray + "def a: FixedArray<3, 3> = x;", DiagnosticCodes.ExpectedTypeArgument);

    [Fact]
    public void Const_Generic_WrongConstType() =>
        ShouldFailWith(
            FixedArray + """def a: FixedArray<Int, "a"> = x;""",
            DiagnosticCodes.ConstArgumentTypeMismatch);

    /// <summary>
    /// Q18: um `def` ligado a um literal é constante e serve de argumento — mas o
    /// checker não dobra expressões, então `1 + 2` ainda não é constante.
    /// </summary>
    [Fact]
    public void Const_Generic_NonConstant() =>
        ShouldFailWith(
            "def n = 1 + 2;\n" + FixedArray + "def a: FixedArray<Int, n> = x;",
            DiagnosticCodes.GenericArgumentNotConstant);

    [Fact]
    public void Const_Generic_FromDefBoundToLiteral() =>
        ShouldPass(
            "def n = 3;\n" + FixedArray
            + "def a: FixedArray<Int, n> = .FixedArray<Int, 3> { values: [1, 2, 3] };");

    /// <summary>Um `def` que só repassa outro `def` continua constante.</summary>
    [Fact]
    public void Const_Generic_PropagatesThroughDefChain() =>
        ShouldPass(
            "def n = 3;\ndef m = n;\n" + FixedArray
            + "def a: FixedArray<Int, m> = .FixedArray<Int, 3> { values: [1, 2, 3] };");

    /// <summary>Um `def` ligado a uma função literal também é constante.</summary>
    [Fact]
    public void Const_Generic_FunctionValueFromDef() =>
        ShouldPass("""
            def Wrapper = type<Make: fn() Int> { tag: Int; };
            def make = fn() Int { return 1; };
            def w = .Wrapper<make> { tag: 0 };
            """);

    /// <summary>Um valor que só existe em execução nunca serve (Q18).</summary>
    [Theory]
    [InlineData("def f = fn(somevar: Int) Int { return scale<somevar>(1); };")]
    [InlineData("def f = fn(v: Int) Int { def n = v; return scale<n>(1); };")]
    public void Const_Generic_RuntimeValue_IsError(string source) =>
        ShouldFailWith(
            "def scale = fn<N: Int>(x: Int) Int { return x * N; };\n" + source,
            DiagnosticCodes.GenericArgumentNotConstant);

    [Fact]
    public void Const_Generic_Missing() =>
        ShouldFailWith(FixedArray + "def a: FixedArray<Int> = x;", DiagnosticCodes.GenericArityMismatch);

    /// <summary>Dentro do corpo, um parâmetro const é um valor do tipo declarado.</summary>
    [Fact]
    public void ConstParameter_IsAValueInTheBody() =>
        TypeOfDef("def scale = fn<N: Int>(x: Int) Int { return x * N; };\ndef r = scale<3>(5);", "r")
            .ShouldBe(PrimitiveType.Int);

    [Fact]
    public void ConstParameter_HasItsDeclaredType() =>
        ShouldFailWith(
            """def f = fn<Label: Str>() Int { return Label; };""",
            DiagnosticCodes.ReturnTypeMismatch);

    // ------------------------------------------- repasse de parâmetro const (Q18)

    private const string Scale = "def scale = fn<N: Int>(x: Int) Int { return x * N; };\n";

    /// <summary>
    /// Q18: um parâmetro const é constante <b>por construção</b> — só recebe
    /// argumentos conhecidos em compilação — então repassá-lo adiante é legítimo.
    /// </summary>
    [Fact]
    public void ConstParameter_CanBeForwarded() =>
        ShouldPass(Scale + """
            def twice = fn<M: Int>(x: Int) Int {
                return scale<M>(x) + scale<M>(x);
            };

            def r = twice<3>(5);
            """);

    [Fact]
    public void ConstParameter_CanBeForwardedToAGenericType() =>
        ShouldPass("""
            def Boxed = type<T, N: Int> { values: T[]; };

            def make = fn<N: Int>(v: Int) Boxed<Int, N> {
                return .Boxed<Int, N> { values: [v] };
            };

            def b: Boxed<Int, 4> = make<4>(9);
            """);

    /// <summary>
    /// A constante simbólica se fecha na instanciação: o retorno de
    /// <c>make&lt;4&gt;</c> é <c>Boxed&lt;Int, 4&gt;</c>, não <c>Boxed&lt;Int, N&gt;</c>.
    /// </summary>
    [Fact]
    public void ConstParameter_IsClosedByInstantiation() =>
        ShouldFailWith(
            """
            def Boxed = type<T, N: Int> { values: T[]; };

            def make = fn<N: Int>(v: Int) Boxed<Int, N> {
                return .Boxed<Int, N> { values: [v] };
            };

            def b: Boxed<Int, 5> = make<4>(9);
            """,
            DiagnosticCodes.TypeMismatch);

    /// <summary>O tipo do parâmetro repassado precisa bater com o que o recebe.</summary>
    [Fact]
    public void ConstParameter_Forwarded_MustMatchTheReceivingType() =>
        ShouldFailWith(
            Scale + "def f = fn<L: Str>(x: Int) Int { return scale<L>(x); };",
            DiagnosticCodes.ConstArgumentTypeMismatch);

    /// <summary>Um parâmetro de tipo não vira argumento const.</summary>
    [Fact]
    public void TypeParameter_IsNotAConstArgument() =>
        ShouldFailWith(
            Scale + "def f = fn<T>(x: Int) Int { return scale<T>(x); };",
            DiagnosticCodes.ExpectedConstArgument);

    /// <summary>Const generics entram na identidade do tipo (plano 06 fase D).</summary>
    [Fact]
    public void Generic_ConstArgs_AffectTypeIdentity()
    {
        const string Source = """
            def Tagged = type<N: Int> { v: Int; };
            def a = .Tagged<3> { v: 1 };
            def b = .Tagged<4> { v: 1 };
            """;

        var types = TypesOfDefs(Source, "a", "b");

        types[0].ShouldNotBe(types[1]);
    }

    /// <summary>O exemplo central da spec §13, com os cinco tipos de argumento.</summary>
    [Fact]
    public void Const_Generic_MixedArgs() =>
        ShouldPass("""
            def SomeType = type<Label: Str, Count: Int, Enabled: Bool, T, Make: fn() Int> {
                label: Str;
            };

            def t = SomeType<"value", 1, true, Int, fn() Int { return 1; }>;
            """);

    /// <summary>Uma função literal é um argumento const legítimo (spec §13).</summary>
    [Fact]
    public void Const_Generic_FunctionValueArg() =>
        ShouldPass("""
            def Wrapper = type<Make: fn() Int> { tag: Int; };
            def w = .Wrapper<fn() Int { return 1; }> { tag: 0 };
            """);

    [Fact]
    public void Const_Generic_FunctionValueArg_WrongSignature() =>
        ShouldFailWith(
            """
            def Wrapper = type<Make: fn() Int> { tag: Int; };
            def w = .Wrapper<fn() Str { return "s"; }> { tag: 0 };
            """,
            DiagnosticCodes.ConstArgumentTypeMismatch);

    /// <summary>
    /// A identidade de um argumento de função é estrutural: duas funções escritas
    /// igual produzem o mesmo tipo.
    /// </summary>
    [Fact]
    public void Const_Generic_FunctionValueArg_IsStructural()
    {
        const string Source = """
            def Wrapper = type<Make: fn() Int> { tag: Int; };
            def a = .Wrapper<fn() Int { return 1; }> { tag: 0 };
            def b = .Wrapper<fn() Int { return 1; }> { tag: 0 };
            def c = .Wrapper<fn() Int { return 2; }> { tag: 0 };
            """;

        var types = TypesOfDefs(Source, "a", "b", "c");

        types[0].ShouldBe(types[1]);
        types[0].ShouldNotBe(types[2]);
    }
}
