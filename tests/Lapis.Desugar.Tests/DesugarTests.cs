using Lapis.Ast;
using Lapis.Ast.Core;

namespace Lapis.Desugar.Tests;

public sealed class SequencingTests : DesugarTestBase
{
    [Fact]
    public void TopLevelDefs_NestAsLets() =>
        ShouldDesugarTo(
            "def a = 1; def b = 2;",
            """
            (block
              (let a
                (lit 1)
              )
              (let b
                (lit 2)
              )
              (tail
                (lit ())
              )
            )
            """);

    [Fact]
    public void TopLevelExpression_UsesSyntheticLet()
    {
        var program = Compile("f();");

        var let = program.Body.ShouldBeOfType<CoreLet>();
        let.IsSynthetic.ShouldBeTrue();
        let.Name.ShouldStartWith("$");
        let.Value.ShouldBeOfType<CoreCall>();
    }

    [Fact]
    public void EmptyFile_IsUnitLiteral() =>
        Compile(string.Empty).Body.ShouldBeOfType<CoreLiteral>().Value.ShouldBeOfType<ConstUnit>();

    [Fact]
    public void Block_WithTail_KeepsTail() =>
        ShouldDesugarTo(
            "{ def x = 1; x };",
            """
            (block
              (stmt
                (block
                  (let x
                    (lit 1)
                  )
                  (tail
                    (var x)
                  )
                )
              )
              (tail
                (lit ())
              )
            )
            """);

    [Fact]
    public void Block_WithoutTail_EndsInUnit()
    {
        var program = Compile("{ f(); };");

        // Let externo (statement top-level) → Let sintético do `f();` → ()
        var outer = program.Body.ShouldBeOfType<CoreLet>();
        var inner = outer.Value.ShouldBeOfType<CoreLet>();
        inner.Body.ShouldBeOfType<CoreLiteral>().Value.ShouldBeOfType<ConstUnit>();
    }

    [Fact]
    public void Block_Empty_IsUnit()
    {
        var program = Compile("{};");

        program.Body.ShouldBeOfType<CoreLet>().Value
            .ShouldBeOfType<CoreLiteral>().Value.ShouldBeOfType<ConstUnit>();
    }

    [Fact]
    public void FreshNames_UsePrefixTheLexerCannotProduce()
    {
        var program = Compile("f(); g(); h();");

        var names = AllNodes(program).OfType<CoreLet>().Where(l => l.IsSynthetic).Select(l => l.Name).ToList();

        names.Count.ShouldBe(3);
        names.ShouldAllBe(n => n.StartsWith('$'));
        names.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void FreshNames_AreDeterministic()
    {
        const string Source = "f(); def x = 1; g();";

        Print(Source).ShouldBe(Print(Source));
    }
}

public sealed class FunctionAndReturnTests : DesugarTestBase
{
    /// <summary>Spec §12: o corpo não ganha <c>return</c> implícito.</summary>
    [Fact]
    public void Function_HasNoImplicitReturn()
    {
        var program = Compile("def f = fn() Int { 1 };");

        var lambda = program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreLambda>();
        lambda.Body.ShouldBeOfType<CoreLiteral>();
        AllNodes(program).OfType<CoreReturn>().ShouldBeEmpty();
    }

    [Fact]
    public void Return_WithValue()
    {
        var program = Compile("def f = fn() Int { return a + b; };");

        AllNodes(program).OfType<CoreReturn>().ShouldHaveSingleItem()
            .Value.ShouldBeOfType<CoreBinary>();
    }

    [Fact]
    public void Return_Empty_HasNullValue()
    {
        var program = Compile("def f = fn() { return; };");

        AllNodes(program).OfType<CoreReturn>().ShouldHaveSingleItem().Value.ShouldBeNull();
    }

    [Fact]
    public void Function_MissingReturnType_StaysNull()
    {
        var program = Compile("def f = fn() { };");

        program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreLambda>().ReturnType.ShouldBeNull();
    }

    [Fact]
    public void NestedFunctions_HaveIndependentReturns()
    {
        var program = Compile("def f = fn() Int { def g = fn() Str { return \"s\"; }; return 1; };");

        AllNodes(program).OfType<CoreLambda>().Count().ShouldBe(2);
        AllNodes(program).OfType<CoreReturn>().Count().ShouldBe(2);
    }

    /// <summary>Spec §12: exemplo <c>abs</c> com retorno antecipado.</summary>
    [Fact]
    public void AbsExample_Snapshot() =>
        ShouldDesugarTo(
            """
            def abs = fn(x: Int) Int {
                if x < 0 {
                    return -x;
                }

                return x;
            };
            """,
            """
            (block
              (let abs
                (lambda ((x Int)) Int
                  (block
                    (stmt
                      (if
                        (<
                          (var x)
                          (lit 0)
                        )
                        (block
                          (stmt
                            (return
                              (-u
                                (var x)
                              )
                            )
                          )
                          (tail
                            (lit ())
                          )
                        )
                        (lit ())
                      )
                    )
                    (stmt
                      (return
                        (var x)
                      )
                    )
                    (tail
                      (lit ())
                    )
                  )
                )
              )
              (tail
                (lit ())
              )
            )
            """);
}

public sealed class ConditionalTests : DesugarTestBase
{
    [Fact]
    public void If_WithoutElse_GetsUnitElse()
    {
        var program = Compile("if c { };");

        var node = AllNodes(program).OfType<CoreIf>().ShouldHaveSingleItem();
        node.Else.ShouldBeOfType<CoreLiteral>().Value.ShouldBeOfType<ConstUnit>();
    }

    [Fact]
    public void ElseIf_Chains()
    {
        var program = Compile("if a { } else if b { } else { };");

        var outer = AllNodes(program).OfType<CoreIf>().First();
        outer.Else.ShouldBeOfType<CoreIf>();
    }

    [Fact]
    public void AndAlso_BecomesIf()
    {
        var program = Compile("def r = a && b;");

        var node = program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreIf>();
        node.Condition.ShouldBeOfType<CoreVariable>().Name.ShouldBe("a");
        node.Then.ShouldBeOfType<CoreVariable>().Name.ShouldBe("b");
        node.Else.ShouldBeOfType<CoreLiteral>().Value.ShouldBe(ConstBool.False);
    }

    [Fact]
    public void OrElse_BecomesIf()
    {
        var program = Compile("def r = a || b;");

        var node = program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreIf>();
        node.Then.ShouldBeOfType<CoreLiteral>().Value.ShouldBe(ConstBool.True);
        node.Else.ShouldBeOfType<CoreVariable>().Name.ShouldBe("b");
    }

    /// <summary>Propriedade: <c>&amp;&amp;</c> e <c>||</c> não sobrevivem na Core.</summary>
    [Theory]
    [InlineData("def r = a && b;")]
    [InlineData("def r = a || b;")]
    [InlineData("def r = a && b || c && d;")]
    [InlineData("def r = !a && (b || c);")]
    public void NoLogicalOperators_SurviveInCore(string source)
    {
        var binaries = AllNodes(Compile(source)).OfType<CoreBinary>();

        binaries.ShouldAllBe(b => b.Operator != BinaryOperator.AndAlso && b.Operator != BinaryOperator.OrElse);
    }
}

public sealed class LiteralNormalizationTests : DesugarTestBase
{
    [Fact]
    public void NegativeInt_BecomesLiteral()
    {
        var program = Compile("def x = -10;");

        program.Body.ShouldBeOfType<CoreLet>().Value
            .ShouldBeOfType<CoreLiteral>().Value.ShouldBe(new ConstInt(-10));
    }

    [Fact]
    public void NegativeFloat_BecomesLiteral()
    {
        var program = Compile("def x = -0.5;");

        program.Body.ShouldBeOfType<CoreLet>().Value
            .ShouldBeOfType<CoreLiteral>().Value.ShouldBe(new ConstFloat(-0.5));
    }

    [Fact]
    public void NegateVariable_StaysUnary()
    {
        var program = Compile("def y = -x;");

        program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreUnary>();
    }

    /// <summary>Normalização, não otimização: dupla negação não é dobrada.</summary>
    [Fact]
    public void DoubleNegation_IsNotFolded()
    {
        var program = Compile("def y = - -x;");

        var outer = program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreUnary>();
        outer.Operand.ShouldBeOfType<CoreUnary>();
    }

    /// <summary>O desugar não faz constant folding — isso é do partial evaluator (spec §58).</summary>
    [Fact]
    public void NoConstantFolding()
    {
        var program = Compile("def x = 1 + 2;");

        program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreBinary>();
    }
}

public sealed class InvariantTests : DesugarTestBase
{
    [Theory]
    [InlineData("def x = 1;")]
    [InlineData("def add = fn(a: Int, b: Int) Int { return a + b; };")]
    [InlineData("if a { b; } else { c; }")]
    [InlineData("{ def x = 1; { def y = 2; x + y } };")]
    [InlineData("f(g(h(1)));")]
    public void EveryNode_HasUniqueNodeId(string source)
    {
        var program = Compile(source);
        var nodes = AllNodes(program);

        nodes.Select(n => n.NodeId).Distinct().Count().ShouldBe(nodes.Count);
        nodes.Count.ShouldBe(program.NodeCount);
    }

    [Theory]
    [InlineData("def x = 1;")]
    [InlineData("def add = fn(a: Int, b: Int) Int { return a + b; };")]
    [InlineData("if a { b; } else { c; }")]
    public void EveryNode_HasSpanWithinFile(string source)
    {
        foreach (var node in AllNodes(Compile(source)))
        {
            node.Span.Start.ShouldBeGreaterThanOrEqualTo(0);
            node.Span.End.ShouldBeLessThanOrEqualTo(source.Length);
        }
    }

    [Fact]
    public void ImplicitElse_PointsAtTheIf()
    {
        const string Source = "if c { };";
        var program = Compile(Source);

        var node = AllNodes(program).OfType<CoreIf>().ShouldHaveSingleItem();

        // Não é SourceSpan.Synthetic: aponta para o `if` que o originou.
        node.Else.Span.ShouldBe(node.Span);
    }

    [Fact]
    public void Desugar_IsDeterministic()
    {
        const string Source = "def add = fn(a: Int, b: Int) Int { return a + b; }; add(1, 2);";

        Print(Source).ShouldBe(Print(Source));
    }
}

public sealed class ArrayAndEnumDesugarTests : DesugarTestBase
{
    [Fact]
    public void Array_IsOneToOne()
    {
        var program = Compile("def a = [1, 2];");

        program.Body.ShouldBeOfType<CoreLet>().Value
            .ShouldBeOfType<CoreArray>().Elements.Length.ShouldBe(2);
    }

    /// <summary>
    /// A checagem de limites é semântica do nó <c>Index</c> (spec §41): o desugar
    /// não a expande num <c>If</c>.
    /// </summary>
    [Fact]
    public void Index_DoesNotExpandBoundsCheck()
    {
        var program = Compile("def r = a[0];");

        program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreIndex>();
        AllNodes(program).OfType<CoreIf>().ShouldBeEmpty();
    }

    [Fact]
    public void Member_IsOneToOne()
    {
        var program = Compile("def v = A.B;");

        program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreField>().Name.ShouldBe("B");
    }

    [Fact]
    public void EnumDef_PreservesVariantsAndTypeParameters()
    {
        var program = Compile("def R = enum<T, E> { Ok(T), Err(E) };");

        var enumDef = program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreEnumDef>();
        enumDef.TypeParameters.Select(p => p.Name).ShouldBe(["T", "E"]);
        enumDef.Variants.Select(v => v.Name).ShouldBe(["Ok", "Err"]);
    }

    [Fact]
    public void Lambda_PreservesGenericParameters()
    {
        var program = Compile("def f = fn<T, N: Int>(v: T) T { return v; };");

        var lambda = program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreLambda>();
        lambda.TypeParameters.Select(p => p.Name).ShouldBe(["T", "N"]);
        lambda.TypeParameters.Select(p => p.IsConst).ShouldBe([false, true]);
    }

    /// <summary>
    /// A instanciação <b>não</b> é achatada na chamada: o partial evaluator
    /// precisa ver onde cada especialização foi pedida (plano 02 §2.2).
    /// </summary>
    [Fact]
    public void Instantiate_SurvivesTheDesugar()
    {
        var program = Compile("f<Int>(1);");

        var call = program.Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreCall>();
        var instantiate = call.Callee.ShouldBeOfType<CoreInstantiate>();

        instantiate.Target.ShouldBeOfType<CoreVariable>().Name.ShouldBe("f");
        instantiate.Arguments.ShouldHaveSingleItem().ShouldBeOfType<CoreNameArgument>().Name.ShouldBe("Int");
    }

    /// <summary>Um argumento const literal chega à Core como expressão, não como tipo.</summary>
    [Fact]
    public void Instantiate_ConstArgumentBecomesAnExpression()
    {
        var program = Compile("f<3>(1);");

        var instantiate = program.Body.ShouldBeOfType<CoreLet>().Value
            .ShouldBeOfType<CoreCall>().Callee.ShouldBeOfType<CoreInstantiate>();

        instantiate.Arguments.ShouldHaveSingleItem()
            .ShouldBeOfType<CoreValueArgument>()
            .Value.ShouldBeOfType<CoreLiteral>().Value.ShouldBe(new ConstInt(3));
    }
}
