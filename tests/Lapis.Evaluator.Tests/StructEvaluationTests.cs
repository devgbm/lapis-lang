namespace Lapis.Evaluator.Tests;

public sealed class StructEvaluationTests : EvaluatorTestBase
{
    private const string User = "def User = type { id: Int; name: Str; };\n";

    [Fact]
    public void Construct_AndPrint() =>
        Output($"{User}print(.User {{ id: 1, name: \"Gabriel\" }});")
            .ShouldBe("User { id: 1, name: \"Gabriel\" }\n");

    [Fact]
    public void FieldAccess() =>
        Output($"{User}def u = .User {{ id: 1, name: \"g\" }};\nprint(u.name);").ShouldBe("g\n");

    [Fact]
    public void FieldAccess_InExpression() =>
        Output($"{User}def u = .User {{ id: 1, name: \"g\" }};\nprint(u.id + 41);").ShouldBe("42\n");

    /// <summary>Campos avaliados na ordem do código, não na ordem de declaração.</summary>
    [Fact]
    public void Construct_EvaluatesFieldsInSourceOrder() =>
        Output($$"""
            {{User}}
            def trace = fn(n: Int) Int {
                print(n);
                return n;
            };

            def u = .User { name: "g", id: trace(1) };
            """).ShouldBe("1\n");

    [Fact]
    public void Construct_FieldOrderDoesNotAffectLayout() =>
        Output($"{User}print(.User {{ name: \"g\", id: 1 }});")
            .ShouldBe("User { id: 1, name: \"g\" }\n");

    [Fact]
    public void Struct_EqualityIsFieldwise() =>
        Output($$"""
            {{User}}
            def a = .User { id: 1, name: "g" };
            def b = .User { id: 1, name: "g" };
            def c = .User { id: 2, name: "g" };

            print(a == b);
            print(a == c);
            """).ShouldBe("true\nfalse\n");

    [Fact]
    public void GenericType_Construct() =>
        Output("def Box = type<T> { value: T; };\nprint(.Box<Int> { value: 42 });")
            .ShouldBe("Box { value: 42 }\n");

    [Fact]
    public void GenericType_FieldAccess() =>
        Output("def Box = type<T> { value: T; };\ndef b = .Box<Str> { value: \"x\" };\nprint(b.value);")
            .ShouldBe("x\n");

    [Fact]
    public void NestedStructs() =>
        Output("""
            def Inner = type { v: Int; };
            def Outer = type { inner: Inner; };

            def o = .Outer { inner: .Inner { v: 7 } };

            print(o.inner.v);
            print(o);
            """).ShouldBe("7\nOuter { inner: Inner { v: 7 } }\n");

    /// <summary>Q2 na prática: construção na condição de `if`, sem parênteses.</summary>
    [Fact]
    public void Construct_InIfCondition() =>
        Output("""
            def Point = type { x: Int; valid: Bool; };

            if .Point { x: 1, valid: true }.valid {
                print("ok");
            }
            """).ShouldBe("ok\n");

    [Fact]
    public void TypeDef_ProducesTypeValue() =>
        Output($"{User}print(User);").ShouldBe("<tipo User>\n");

    /// <summary>Spec §14: o exemplo `User`.</summary>
    [Fact]
    public void Section14_UserExample() =>
        Output("""
            def User = type {
                id: Int;
                name: Str;
            };

            def user = .User {
                id: 1,
                name: "Gabriel"
            };

            print(user.name);
            """).ShouldBe("Gabriel\n");
}
