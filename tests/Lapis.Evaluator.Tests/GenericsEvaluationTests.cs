namespace Lapis.Evaluator.Tests;

/// <summary>
/// Generics em execução. O checker não monomorfiza nada (plano 06 §6.8), então o
/// evaluator vê uma closure comum — <b>exceto</b> pelos parâmetros const, que são
/// valores de verdade dentro do corpo (spec §13).
/// </summary>
public sealed class GenericEvaluationTests : EvaluatorTestBase
{
    private const string Identity = "def identity = fn<T>(value: T) T { return value; };\n";

    [Fact]
    public void GenericFunction_ReturnsItsArgument() =>
        Output(Identity + """
            print(identity<Int>(10));
            print(identity<Str>("olá"));
            """).ShouldBe("10\nolá\n");

    /// <summary>Instanciar não avalia o alvo duas vezes nem muda o valor.</summary>
    [Fact]
    public void Instantiation_PreservesTheClosure() =>
        Output(Identity + """
            def f = identity<Int>;

            print(f(1));
            print(f(2));
            """).ShouldBe("1\n2\n");

    [Fact]
    public void GenericStruct_Constructs() =>
        Output("""
            def Box = type<T> { value: T; };
            def b = .Box<Str> { value: "dentro" };

            print(b);
            print(b.value);
            """).ShouldBe("Box { value: \"dentro\" }\ndentro\n");

    [Fact]
    public void GenericVariant_Constructs() =>
        Eval("Result<Int, IndexError>.Ok(42)").ShouldBe("Result.Ok(42)");

    [Fact]
    public void GenericVariant_Nullary() =>
        Output("""
            def Option = enum<T> { Some(T), None };

            print(Option<Int>.None);
            print(Option<Int>.Some(5));
            """).ShouldBe("Option.None\nOption.Some(5)\n");

    /// <summary>Um valor construído com argumentos genéricos casa em <c>match</c>.</summary>
    [Fact]
    public void GenericVariant_Matches() =>
        Output("""
            def Option = enum<T> { Some(T), None };

            def unwrap = fn(o: Option<Int>, fallback: Int) Int {
                match o {
                    Option.Some(v) => return v,
                    Option.None => return fallback
                }
            };

            print(unwrap(Option<Int>.Some(7), 0));
            print(unwrap(Option<Int>.None, 0));
            """).ShouldBe("7\n0\n");
}

/// <summary>Const generics: o parâmetro chega ao corpo como valor.</summary>
public sealed class ConstGenericEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void ConstParameter_IsBoundInTheBody() =>
        Output("""
            def scale = fn<N: Int>(x: Int) Int {
                return x * N;
            };

            print(scale<3>(5));
            print(scale<10>(5));
            """).ShouldBe("15\n50\n");

    /// <summary>
    /// Duas instanciações da mesma função genérica são independentes: cada uma
    /// carrega seu próprio valor const.
    /// </summary>
    [Fact]
    public void Instantiations_DoNotShareConstBindings() =>
        Output("""
            def tag = fn<Label: Str>(x: Int) Str {
                return Label;
            };

            def a = tag<"a">;
            def b = tag<"b">;

            print(a(1));
            print(b(1));
            print(a(1));
            """).ShouldBe("a\nb\na\n");

    [Fact]
    public void ConstFunctionParameter_IsCallable() =>
        Output("""
            def apply = fn<Make: fn() Int>(bonus: Int) Int {
                return Make() + bonus;
            };

            print(apply<fn() Int { return 40; }>(2));
            """).ShouldBe("42\n");

    /// <summary>Um parâmetro const de tipo e um de valor convivem na mesma função.</summary>
    [Fact]
    public void MixedTypeAndConstParameters() =>
        Output("""
            def repeat = fn<T, N: Int>(value: T) T[] {
                if N == 2 {
                    return [value, value];
                }

                return [value];
            };

            print(repeat<Int, 2>(7));
            print(repeat<Str, 1>("x"));
            """).ShouldBe("[7, 7]\n[\"x\"]\n");

    /// <summary>Parâmetros const são capturados como qualquer outro binding.</summary>
    [Fact]
    public void ConstParameter_IsCapturedByInnerClosures() =>
        Output("""
            def make = fn<N: Int>() fn(Int) Int {
                return fn(x: Int) Int {
                    return x + N;
                };
            };

            def addFive = make<5>();

            print(addFive(1));
            """).ShouldBe("6\n");
}
