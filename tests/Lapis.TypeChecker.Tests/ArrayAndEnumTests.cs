using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

public sealed class SpanTypeTests : TypeCheckerTestBase
{
    /// <summary>
    /// O tamanho entra no tipo (plano 24): um literal de três elementos é
    /// <c>[Int;3]</c>, não <c>[Int;?]</c>.
    /// </summary>
    [Fact]
    public void Homogeneous_TypesAsSpanWithSize() =>
        TypeOfFirstDef("def a = .[1, 2, 3];").ToDisplayString().ShouldBe("[Int;3]");

    [Fact]
    public void Nested_TypesAsSpanOfSpan() =>
        TypeOfFirstDef("def a = .[.[1], .[2]];").ToDisplayString().ShouldBe("[[Int;1];2]");

    [Fact]
    public void OfStrings() => TypeOfFirstDef("def a = .[\"x\"];").ToDisplayString().ShouldBe("[Str;1]");

    /// <summary>Spec §18: spans heterogêneos são inválidos.</summary>
    [Fact]
    public void Heterogeneous_ReportsLap0240() =>
        ShouldFailWith("def a = .[1, \"x\"];", DiagnosticCodes.HeterogeneousArray);

    [Fact]
    public void Heterogeneous_ThreeKinds_ReportsLap0240() =>
        ShouldFailWith("def a = .[1, \"hello\", true];", DiagnosticCodes.HeterogeneousArray);

    [Fact]
    public void Empty_WithoutAnnotation_ReportsLap0241() =>
        ShouldFailWith("def a = .[];", DiagnosticCodes.EmptyArrayNeedsAnnotation);

    [Fact]
    public void Annotation_Matching_IsAccepted() => ShouldPass("def a: [Int;3] = .[1, 2, 3];");

    /// <summary>Q29: o tamanho desconhecido aceita qualquer tamanho conhecido.</summary>
    [Fact]
    public void Annotation_UnknownSize_AcceptsKnownSize() => ShouldPass("def a: [Int;?] = .[1, 2, 3];");

    /// <summary>E o contrário não vale: <c>?</c> não vira um tamanho fixo.</summary>
    [Fact]
    public void Annotation_KnownSize_RejectsUnknownSize() =>
        ShouldFailWith("def a: [Int;?] = .[1]; def b: [Int;1] = a;", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void Annotation_WrongSize_ReportsLap0210() =>
        ShouldFailWith("def a: [Int;2] = .[1, 2, 3];", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void Annotation_MismatchedElement_ReportsLap0210() =>
        ShouldFailWith("def a: [Str;1] = .[1];", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void OfFunctions_IsAccepted() =>
        ShouldPass("def fs = .[fn(x: Int) Int { return x + 1; }, fn(x: Int) Int { return x * 2; }];");

    /// <summary>
    /// Um <c>var</c> pode ser reatribuído a um span de outro tamanho, então o
    /// tamanho sai do tipo já na declaração (plano 24 §24.4). O literal continua
    /// sendo <c>[Int;3]</c>; quem alarga é a ligação, e é por isso que o teste
    /// olha para o **uso** e não para o valor.
    /// </summary>
    [Fact]
    public void Var_WidensToUnknownSize() =>
        TypeOfDef("var a = .[1, 2, 3];\ndef b = a;", "b").ToDisplayString().ShouldBe("[Int;?]");

    /// <summary>E um <c>def</c> não alarga: o tamanho fica no tipo.</summary>
    [Fact]
    public void Def_KeepsTheSize() =>
        TypeOfDef("def a = .[1, 2, 3];\ndef b = a;", "b").ToDisplayString().ShouldBe("[Int;3]");

    [Fact]
    public void Var_CanBeReassignedToAnotherSize() =>
        ShouldPass("var a = .[1]; a = .[1, 2, 3];");

    /// <summary>
    /// Anotado, o <c>var</c> mantém o tamanho — e aí a reatribuição passa a ser
    /// checada. É o que dá indexação total num binding mutável.
    /// </summary>
    [Fact]
    public void VarAnnotated_KeepsTheSize() =>
        ShouldFailWith("var a: [Int;3] = .[1, 2, 3]; a = .[4, 5];", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void VarAnnotated_IndexesTotally() =>
        TypeOfDef("var a: [Int;3] = .[1, 2, 3];\ndef b = a[1];", "b").ShouldBe(PrimitiveType.Int);

    // ------------------------------------------------ span por repetição

    /// <summary>
    /// Com a quantidade constante, o tamanho entra no tipo como em qualquer
    /// literal — e é o que faz a indexação por índice literal voltar a ser total.
    /// </summary>
    [Fact]
    public void Repeat_ConstantSize_PutsTheSizeInTheType() =>
        TypeOfFirstDef("def a = .[Int; 0; 8];").ToDisplayString().ShouldBe("[Int;8]");

    [Fact]
    public void Repeat_ConstantSizeFromDef_PutsTheSizeInTheType() =>
        TypeOfDef("def n = 3;\ndef a = .[Str; \"x\"; n];", "a").ToDisplayString().ShouldBe("[Str;3]");

    /// <summary>E sem a quantidade constante, o tipo não fala do tamanho.</summary>
    [Fact]
    public void Repeat_DynamicSize_IsUnknownSize() =>
        TypeOfDef("var n = 3;\ndef a = .[Int; 0; n];", "a").ToDisplayString().ShouldBe("[Int;?]");

    [Fact]
    public void Repeat_ConstantSize_IndexesTotally() =>
        TypeOfDef("def a = .[Int; 0; 8];\ndef b = a[7];", "b").ShouldBe(PrimitiveType.Int);

    [Fact]
    public void Repeat_ConstantSize_OutOfBounds_ReportsLap0244() =>
        ShouldFailWith("def a = .[Int; 0; 8]; def b = a[8];", DiagnosticCodes.IndexOutOfBounds);

    /// <summary>
    /// O elemento vem da anotação escrita, não do inicializador: sem inferência
    /// (Q7), `Option&lt;Int&gt;.None` não determina sozinho o tipo do span.
    /// </summary>
    [Fact]
    public void Repeat_ElementComesFromTheAnnotation() =>
        TypeOfFirstDef("def a = .[Option<Int>; Option<Int>.None; 3];")
            .ToDisplayString().ShouldBe("[Option<Int>;3]");

    [Fact]
    public void Repeat_InitializerMustFitTheElement() =>
        ShouldFailWith("""def a = .[Int; "x"; 3];""", DiagnosticCodes.TypeMismatch);

    /// <summary>O inicializador cabe pela relação, não pela igualdade (Q29).</summary>
    [Fact]
    public void Repeat_InitializerUsesAssignability() =>
        ShouldPass("def a = .[[Int;?]; .[1, 2, 3]; 2];");

    [Fact]
    public void Repeat_SizeMustBeInt() =>
        ShouldFailWith("""def a = .[Int; 0; "x"];""", DiagnosticCodes.IndexMustBeInt);

    [Fact]
    public void Repeat_UnknownElement_ReportsLap0204() =>
        ShouldFailWith("def a = .[Naotem; 0; 3];", DiagnosticCodes.UnknownType);

    [Fact]
    public void Repeat_ZeroSize_IsAccepted() =>
        TypeOfFirstDef("def a = .[Int; 0; 0];").ToDisplayString().ShouldBe("[Int;0]");

    /// <summary>
    /// Quantidade negativa é span vazio, não erro — a mesma escolha da divisão
    /// inteira por zero (Q9). Reportar quebraria o partial evaluator: `0 - 1` não
    /// é constante aqui, e dobrar a subtração transformaria um programa que
    /// compila num que não compila.
    /// </summary>
    [Fact]
    public void Repeat_NegativeSize_IsEmpty() =>
        TypeOfFirstDef("def a = .[Int; 0; -1];").ToDisplayString().ShouldBe("[Int;0]");

    /// <summary>O `var` alarga a repetição como alarga qualquer span.</summary>
    [Fact]
    public void Repeat_InVar_Widens() =>
        TypeOfDef("var a = .[Int; 0; 8];\ndef b = a;", "b").ToDisplayString().ShouldBe("[Int;?]");

    [Fact]
    public void Repeat_InAnnotatedVar_KeepsTheSize() =>
        ShouldFailWith("var a: [Int;3] = .[Int; 0; 3]; a = .[4, 5];", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void Repeat_AnnotationChecksTheSize() =>
        ShouldFailWith("def a: [Int;3] = .[Int; 0; 4];", DiagnosticCodes.TypeMismatch);

    // ---------------------------------------------------------- length

    /// <summary>
    /// Sobre <c>[T;N]</c>, <c>length</c> é a constante <c>N</c>, dobrada pelo
    /// checker — e por isso serve de argumento const genérico (Q18).
    /// </summary>
    [Fact]
    public void Length_OnSizedSpan_IsAConstant() =>
        ShouldPass("""
            def escala = fn<N: Int>(x: Int) Int { return x * N; };

            def a = .[1, 2, 3];
            def n = a.length;

            def r = escala<n>(5);
            """);

    /// <summary>E sobre <c>[T;?]</c> é um <c>Int</c> comum, que não é constante.</summary>
    [Fact]
    public void Length_OnUnknownSize_IsNotAConstant() =>
        ShouldFailWith(
            """
            def escala = fn<N: Int>(x: Int) Int { return x * N; };

            var a = .[1, 2, 3];
            def n = a.length;

            def r = escala<n>(5);
            """,
            DiagnosticCodes.GenericArgumentNotConstant);

    [Fact]
    public void Length_IsAnInt() =>
        TypeOfDef("var a = .[1, 2, 3];\ndef n = a.length;", "n").ShouldBe(PrimitiveType.Int);

    [Fact]
    public void Length_OnNonSpan_ReportsLap0250() =>
        ShouldFailWith("def T = type { x: Int; };\ndef t = .T { x: 1 };\ndef n = t.length;",
            DiagnosticCodes.UnknownField);
}

public sealed class IndexTypeTests : TypeCheckerTestBase
{
    /// <summary>
    /// Com o tamanho no tipo e o índice conhecido, a indexação é <b>total</b>: o
    /// elemento sai sem envelope, porque a checagem de limites já aconteceu aqui
    /// (plano 24 §24.5).
    /// </summary>
    [Fact]
    public void Index_KnownSizeAndConstantIndex_IsTotal() =>
        TypeOfFirstDef("def r = .[1, 2, 3][0];").ToDisplayString().ShouldBe("Int");

    /// <summary>
    /// A regra da spec §21 sobrevive onde o tamanho não é conhecido — só que o
    /// envelope agora é <c>Option</c> (Q31), não <c>Result</c>.
    /// </summary>
    [Fact]
    public void Index_UnknownSize_TypesAsOption() =>
        TypeOfDef("var a = .[1, 2, 3];\ndef r = a[0];", "r")
            .ToDisplayString().ShouldBe("Option<Int>");

    [Fact]
    public void Index_UnknownSize_OfStrSpan() =>
        TypeOfDef("var a = .[\"a\"];\ndef r = a[0];", "r")
            .ToDisplayString().ShouldBe("Option<Str>");

    [Fact]
    public void Index_Nested_UnwrapsInnerSpan() =>
        TypeOfFirstDef("def r = .[.[1]][0];").ToDisplayString().ShouldBe("[Int;1]");

    [Fact]
    public void Index_OptionCanBeAnnotated() =>
        ShouldPass("var a = .[1, 2, 3];\ndef r: Option<Int> = a[0];");

    /// <summary>
    /// Onde o tamanho não é conhecido não há unwrap implícito: o `Option` precisa
    /// ser tratado.
    /// </summary>
    [Fact]
    public void Index_UnknownSize_IsNotImplicitlyUnwrapped() =>
        ShouldFailWith("var a = .[1, 2, 3];\ndef x: Int = a[0];", DiagnosticCodes.TypeMismatch);

    /// <summary>
    /// E com o tamanho conhecido o índice fora dos limites é erro de compilação,
    /// não um <c>None</c> em execução (plano 24 §24.5).
    /// </summary>
    [Fact]
    public void Index_KnownSize_OutOfBounds_ReportsLap0244() =>
        ShouldFailWith("def a = .[1, 2, 3]; def x = a[3];", DiagnosticCodes.IndexOutOfBounds);

    [Fact]
    public void Index_KnownSize_JustPastTheEnd_ReportsLap0244() =>
        ShouldFailWith("def a = .[1]; def x = a[1];", DiagnosticCodes.IndexOutOfBounds);

    /// <summary>
    /// O índice constante vem de um <c>def</c> ligado a literal, não só de um
    /// literal escrito na posição — é a mesma noção de constante da Q18.
    /// </summary>
    [Fact]
    public void Index_KnownSize_ConstantFromDef_ReportsLap0244() =>
        ShouldFailWith("def i = 5; def a = .[1]; def x = a[i];", DiagnosticCodes.IndexOutOfBounds);

    [Fact]
    public void Index_NonIntIndex_ReportsLap0243() =>
        ShouldFailWith("def r = .[1][\"a\"];", DiagnosticCodes.IndexMustBeInt);

    [Fact]
    public void Index_NonSpanTarget_ReportsLap0242() =>
        ShouldFailWith("def r = 1[0];", DiagnosticCodes.NotIndexable);

    /// <summary>
    /// Índice que não é constante volta a ser parcial mesmo com o tamanho no
    /// tipo: a checagem de limites precisa de um número, não de uma expressão
    /// qualquer.
    /// </summary>
    [Fact]
    public void Index_KnownSizeWithVariableIndex_TypesAsOption() =>
        TypeOfDef("var i = 0;\ndef r = .[1, 2][i];", "r").ToDisplayString().ShouldBe("Option<Int>");

    [Fact]
    public void Index_ExpressionIndex_IsAccepted() => ShouldPass("def i = 1; def r = .[1, 2][i + 0];");
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
    public void Result_IsAvailable() => ShouldPass("def f = fn(r: Result<Int, Str>) Void { };");

    [Fact]
    public void Option_IsAvailable() => ShouldPass("def f = fn(o: Option<Int>) Void { };");

    [Fact]
    public void Option_VariantsAreAvailable() => ShouldPass("def o: Option<Int> = Option<Int>.None;");

    [Fact]
    public void PreludeNames_CanBeShadowed() =>
        ShouldPass("def Option = enum { Outra }; def e: Option = Option.Outra;");

    /// <summary>
    /// Sombrear `Option` não muda a semântica de `[]`: a indexação usa a definição
    /// do prelude, resolvida por identidade (plano 09 §9.4).
    /// </summary>
    [Fact]
    public void ShadowingOption_DoesNotChangeIndexing() =>
        TypeOfDef("def Option = enum { Outra };\nvar a = .[1];\ndef r = a[0];", "r")
            .ToDisplayString().ShouldBe("Option<Int>");
}
