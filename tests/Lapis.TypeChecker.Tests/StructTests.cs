using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

public sealed class StructTypeTests : TypeCheckerTestBase
{
    private const string User = "def User = type { id: Int; name: Str; };\n";

    [Fact]
    public void TypeDef_ProducesMetaType() =>
        TypeOfFirstDef(User).ToDisplayString().ShouldBe("<tipo User>");

    [Fact]
    public void Construct_ProducesInstanceType() =>
        TypeOfDef($"{User}def u = .User {{ id: 1, name: \"g\" }};", "u")
            .ToDisplayString().ShouldBe("User");

    [Fact]
    public void Construct_MissingField_ReportsLap0253() =>
        ShouldFailWith($"{User}def u = .User {{ id: 1 }};", DiagnosticCodes.MissingField);

    [Fact]
    public void Construct_ExtraField_ReportsLap0254() =>
        ShouldFailWith(
            $"{User}def u = .User {{ id: 1, name: \"g\", zzz: 2 }};",
            DiagnosticCodes.ExtraField);

    [Fact]
    public void Construct_DuplicateField_ReportsLap0255() =>
        ShouldFailWith(
            $"{User}def u = .User {{ id: 1, id: 2, name: \"g\" }};",
            DiagnosticCodes.DuplicateFieldInitializer);

    [Fact]
    public void Construct_WrongFieldType_ReportsLap0222() =>
        ShouldFailWith(
            $"{User}def u = .User {{ id: \"x\", name: \"g\" }};",
            DiagnosticCodes.ArgumentTypeMismatch);

    [Fact]
    public void Construct_UnknownType_ReportsLap0252() =>
        ShouldFailWith("def u = .Zzz { a: 1 };", DiagnosticCodes.NotConstructible);

    [Fact]
    public void Construct_OnEnum_ReportsLap0252() =>
        ShouldFailWith("def C = enum { Red };\ndef u = .C { a: 1 };", DiagnosticCodes.NotConstructible);

    [Fact]
    public void Construct_FieldOrderIsIrrelevant() =>
        ShouldPass($"{User}def u = .User {{ name: \"g\", id: 1 }};");

    [Fact]
    public void FieldAccess_HasFieldType() =>
        ShouldPass($"{User}def u = .User {{ id: 1, name: \"g\" }};\ndef n: Str = u.name;");

    [Fact]
    public void FieldAccess_WrongAnnotation_ReportsLap0210() =>
        ShouldFailWith(
            $"{User}def u = .User {{ id: 1, name: \"g\" }};\ndef n: Int = u.name;",
            DiagnosticCodes.TypeMismatch);

    [Fact]
    public void FieldAccess_UnknownField_ReportsLap0250() =>
        ShouldFailWith(
            $"{User}def u = .User {{ id: 1, name: \"g\" }};\ndef n = u.zzz;",
            DiagnosticCodes.UnknownField);

    [Fact]
    public void DuplicateFieldDeclaration_ReportsLap0202() =>
        ShouldFailWith("def T = type { a: Int; a: Str; };", DiagnosticCodes.DuplicateDefinition);

    /// <summary>Identidade nominal: dois `type` com a mesma forma são tipos distintos.</summary>
    [Fact]
    public void DistinctTypes_AreNotInterchangeable() =>
        ShouldFailWith(
            "def A = type { v: Int; };\ndef B = type { v: Int; };\ndef x: A = .B { v: 1 };",
            DiagnosticCodes.TypeMismatch);

    [Fact]
    public void GenericType_Construct() =>
        ShouldPass("def Box = type<T> { value: T; };\ndef b: Box<Int> = .Box<Int> { value: 1 };");

    [Fact]
    public void GenericType_FieldHasSubstitutedType() =>
        ShouldFailWith(
            "def Box = type<T> { value: T; };\ndef b = .Box<Int> { value: 1 };\ndef v: Str = b.value;",
            DiagnosticCodes.TypeMismatch);

    [Fact]
    public void GenericType_WrongPayloadType_ReportsLap0222() =>
        ShouldFailWith(
            "def Box = type<T> { value: T; };\ndef b = .Box<Int> { value: \"x\" };",
            DiagnosticCodes.ArgumentTypeMismatch);

    [Fact]
    public void GenericType_MissingArguments_ReportsLap0295() =>
        ShouldFailWith(
            "def Box = type<T> { value: T; };\ndef b = .Box { value: 1 };",
            DiagnosticCodes.GenericTypeNeedsArguments);

    [Fact]
    public void GenericType_DistinctInstantiations_AreDistinct() =>
        ShouldFailWith(
            "def Box = type<T> { value: T; };\ndef b: Box<Str> = .Box<Int> { value: 1 };",
            DiagnosticCodes.TypeMismatch);

    /// <summary>
    /// Q2: com ponto inicial não há ambiguidade com o `{` de `if`, então a
    /// construção é válida na condição sem parênteses.
    /// </summary>
    [Fact]
    public void Construct_InIfCondition_NeedsNoParentheses() =>
        ShouldPass("""
            def Point = type { x: Int; valid: Bool; };

            if .Point { x: 1, valid: true }.valid {
                print("ok");
            }
            """);

    [Fact]
    public void Struct_EqualityIsFieldwise() =>
        ShouldPass($"{User}def a = .User {{ id: 1, name: \"g\" }};\ndef b = a == a;");
}
