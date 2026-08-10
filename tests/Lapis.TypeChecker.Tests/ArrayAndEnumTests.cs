using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

public sealed class ArrayTypeTests : TypeCheckerTestBase
{
    [Fact]
    public void Homogeneous_TypesAsArray() =>
        TypeOfFirstDef("def a = [1, 2, 3];").ToDisplayString().ShouldBe("Int[]");

    [Fact]
    public void Nested_TypesAsArrayOfArray() =>
        TypeOfFirstDef("def a = [[1], [2]];").ToDisplayString().ShouldBe("Int[][]");

    [Fact]
    public void OfStrings() => TypeOfFirstDef("def a = [\"x\"];").ToDisplayString().ShouldBe("Str[]");

    /// <summary>Spec §18: arrays heterogêneos são inválidos.</summary>
    [Fact]
    public void Heterogeneous_ReportsLap0240() =>
        ShouldFailWith("def a = [1, \"x\"];", DiagnosticCodes.HeterogeneousArray);

    [Fact]
    public void Heterogeneous_ThreeKinds_ReportsLap0240() =>
        ShouldFailWith("def a = [1, \"hello\", true];", DiagnosticCodes.HeterogeneousArray);

    [Fact]
    public void Empty_WithoutAnnotation_ReportsLap0241() =>
        ShouldFailWith("def a = [];", DiagnosticCodes.EmptyArrayNeedsAnnotation);

    [Fact]
    public void Annotation_Matching_IsAccepted() => ShouldPass("def a: Int[] = [1, 2, 3];");

    [Fact]
    public void Annotation_Mismatched_ReportsLap0210() =>
        ShouldFailWith("def a: Str[] = [1];", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void OfFunctions_IsAccepted() =>
        ShouldPass("def fs = [fn(x: Int) Int { return x + 1; }, fn(x: Int) Int { return x * 2; }];");
}

public sealed class IndexTypeTests : TypeCheckerTestBase
{
    /// <summary>
    /// A regra fundamental da spec §21: indexar devolve <c>Result</c>, não o
    /// elemento. Este é o teste central do M2.
    /// </summary>
    [Fact]
    public void Index_TypesAsResultOfElementAndIndexError() =>
        TypeOfFirstDef("def r = [1, 2, 3][0];")
            .ToDisplayString().ShouldBe("Result<Int, IndexError>");

    [Fact]
    public void Index_OfStrArray() =>
        TypeOfFirstDef("def r = [\"a\"][0];")
            .ToDisplayString().ShouldBe("Result<Str, IndexError>");

    [Fact]
    public void Index_Nested_WrapsInnerArray() =>
        TypeOfFirstDef("def r = [[1]][0];")
            .ToDisplayString().ShouldBe("Result<Int[], IndexError>");

    [Fact]
    public void Index_ResultCanBeAnnotated() =>
        ShouldPass("def r: Result<Int, IndexError> = [1, 2, 3][0];");

    /// <summary>Não há unwrap implícito: o `Result` precisa ser tratado.</summary>
    [Fact]
    public void Index_IsNotImplicitlyUnwrapped() =>
        ShouldFailWith("def x: Int = [1, 2, 3][0];", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void Index_NonIntIndex_ReportsLap0243() =>
        ShouldFailWith("def r = [1][\"a\"];", DiagnosticCodes.IndexMustBeInt);

    [Fact]
    public void Index_NonArrayTarget_ReportsLap0242() =>
        ShouldFailWith("def r = 1[0];", DiagnosticCodes.NotIndexable);

    [Fact]
    public void Index_ExpressionIndex_IsAccepted() => ShouldPass("def i = 1; def r = [1, 2][i + 0];");
}

public sealed class EnumTypeTests : TypeCheckerTestBase
{
    [Fact]
    public void EnumDef_ProducesMetaType() =>
        TypeOfFirstDef("def Color = enum { Red, Green };").ToDisplayString().ShouldBe("<tipo Color>");

    [Fact]
    public void NullaryVariant_HasEnumType() =>
        ShouldPass("def Color = enum { Red }; def c: Color = Color.Red;");

    [Fact]
    public void VariantWithPayload_IsAConstructor() =>
        ShouldPass("def Box = enum { Wrap(Int) }; def b: Box = Box.Wrap(1);");

    [Fact]
    public void VariantConstructor_ChecksArgumentType() =>
        ShouldFailWith(
            "def Box = enum { Wrap(Int) }; def b = Box.Wrap(\"x\");",
            DiagnosticCodes.ArgumentTypeMismatch);

    [Fact]
    public void UnknownVariant_ReportsLap0251() =>
        ShouldFailWith("def Color = enum { Red }; def c = Color.Purple;", DiagnosticCodes.UnknownVariant);

    /// <summary>Q3: a forma nua não existe — `Red` é apenas uma variável inexistente.</summary>
    [Fact]
    public void BareVariant_ReportsLap0201() =>
        ShouldFailWith("def Color = enum { Red }; def c = Red;", DiagnosticCodes.UnknownVariable);

    [Fact]
    public void DuplicateVariant_ReportsLap0202() =>
        ShouldFailWith("def Color = enum { Red, Red };", DiagnosticCodes.DuplicateDefinition);

    [Fact]
    public void DistinctEnums_AreDistinctTypes() =>
        ShouldFailWith(
            "def A = enum { X }; def B = enum { X }; def v: A = B.X;",
            DiagnosticCodes.TypeMismatch);

    [Fact]
    public void GenericEnum_CanBeInstantiatedInAnnotations() =>
        ShouldPass("def Pair = enum<T> { One(T) }; def f = fn(p: Pair<Int>) Void { };");

    [Fact]
    public void GenericEnum_WithoutArguments_ReportsLap0295() =>
        ShouldFailWith(
            "def Pair = enum<T> { One(T) }; def f = fn(p: Pair) Void { };",
            DiagnosticCodes.GenericTypeNeedsArguments);

    [Fact]
    public void GenericEnum_WrongArity_ReportsLap0290() =>
        ShouldFailWith(
            "def Pair = enum<T> { One(T) }; def f = fn(p: Pair<Int, Str>) Void { };",
            DiagnosticCodes.GenericArityMismatch);

    /// <summary>
    /// Construir variante de enum genérico exige os argumentos de tipo, e sem
    /// inferência (Q7) não há de onde tirá-los — a sintaxe chega no M4.
    /// </summary>
    [Fact]
    public void GenericVariantConstruction_ReportsLap0298() =>
        ShouldFailWith("def r = Result.Ok(1);", DiagnosticCodes.CannotDetermineGenericArguments);
}

public sealed class PreludeIntegrationTests : TypeCheckerTestBase
{
    [Fact]
    public void Result_IsAvailable() => ShouldPass("def f = fn(r: Result<Int, IndexError>) Void { };");

    [Fact]
    public void IndexError_IsAvailable() => ShouldPass("def e: IndexError = IndexError.OutOfBounds;");

    [Fact]
    public void Option_IsAvailable() => ShouldPass("def f = fn(o: Option<Int>) Void { };");

    [Fact]
    public void PreludeNames_CanBeShadowed() =>
        ShouldPass("def IndexError = enum { Outra }; def e: IndexError = IndexError.Outra;");

    /// <summary>
    /// Sombrear `Result` não muda a semântica de `[]`: a indexação usa a definição
    /// do prelude, resolvida por identidade (plano 09 §9.4).
    /// </summary>
    [Fact]
    public void ShadowingResult_DoesNotChangeIndexing() =>
        TypeOfDef("def Result = enum { Outra };\ndef r = [1][0];", "r")
            .ToDisplayString().ShouldBe("Result<Int, IndexError>");
}
