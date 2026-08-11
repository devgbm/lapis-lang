using Lapis.Ast.Surface;
using Lapis.Diagnostics;
using Lapis.Lexer;

namespace Lapis.Macros.Tests;

/// <summary>Declaração de macro e delimitação da invocação (plano 17 §17.2–§17.3).</summary>
public sealed class MacroParseTests : MacroTestBase
{
    [Fact]
    public void Macro_SingleRule()
    {
        var declaration = Declaration("macro m match Expression:e expand { e };");

        declaration.Name.ShouldBe("m");
        declaration.Rules.ShouldHaveSingleItem()
            .Pattern.ShouldBeOfType<PatternSequence>()
            .Items.ShouldHaveSingleItem()
            .ShouldBeOfType<PatternCapture>()
            .Category.ShouldBe(SyntaxCategory.Expression);
    }

    [Fact]
    public void Macro_MultipleRules() =>
        Declaration("""
            macro m
                match Expression:e expand { e }
                match Identifier:i Block:b expand { b };
            """).Rules.Length.ShouldBe(2);

    /// <summary>
    /// <c>in</c> pertence à sintaxe <b>desta</b> macro. Não vira palavra reservada
    /// da linguagem — é o que permite a uma biblioteca definir construções próprias
    /// sem tocar no lexer.
    /// </summary>
    [Fact]
    public void Macro_PatternWithLiteral()
    {
        var items = Declaration("macro m match Identifier:i in Expression:c Block:b expand { b };")
            .Rules[0].Pattern.ShouldBeOfType<PatternSequence>().Items;

        items.Length.ShouldBe(4);
        items[1].ShouldBeOfType<PatternLiteral>().Text.ShouldBe("in");
    }

    [Fact]
    public void Macro_PatternWithRepeat()
    {
        var repeat = Declaration("macro m match Type:t* separado por , expand { 1 };")
            .Rules[0].Pattern.ShouldBeOfType<PatternSequence>()
            .Items.ShouldHaveSingleItem()
            .ShouldBeOfType<PatternRepeat>();

        repeat.Separator.ShouldBe(",");
        repeat.Item.ShouldBeOfType<PatternCapture>().Category.ShouldBe(SyntaxCategory.Type);
    }

    /// <summary>
    /// A repetição de grupo é o que evita inventar categorias sintáticas sob
    /// medida para cada macro.
    /// </summary>
    [Fact]
    public void Macro_PatternWithGroupRepeat()
    {
        var repeat = Declaration("macro m match (Identifier:campo Type:tipo)* separado por , expand { 1 };")
            .Rules[0].Pattern.ShouldBeOfType<PatternSequence>()
            .Items.ShouldHaveSingleItem()
            .ShouldBeOfType<PatternRepeat>();

        repeat.Item.ShouldBeOfType<PatternSequence>().Items.Length.ShouldBe(2);
    }

    [Fact]
    public void Macro_WithConstraint() =>
        Declaration("macro m match Expression:e constraint { true } expand { e };")
            .Rules[0].Constraint.ShouldNotBeNull();

    [Fact]
    public void Macro_UnknownCategory() =>
        ParseWithDiagnostics("macro m match Foo:x expand { x };")
            .Diagnostics.Select(d => d.Code)
            .ShouldContain(DiagnosticCodes.UnknownSyntaxCategory);

    [Fact]
    public void Macro_RequiresExpand() =>
        ParseWithDiagnostics("macro m match Expression:e;")
            .Diagnostics.Select(d => d.Code)
            .ShouldContain(DiagnosticCodes.UnexpectedToken);

    /// <summary>Macro não é valor (Q19): `def m = macro ...` não parseia.</summary>
    [Fact]
    public void Macro_IsNotAValue() =>
        ParseWithDiagnostics("def m = macro n match Expression:e expand { e };")
            .Diagnostics.ShouldNotBeEmpty();

    /// <summary>
    /// Macros ocupam um espaço de nomes próprio: <c>@log</c> e <c>log</c> nunca se
    /// confundem, então os dois convivem.
    /// </summary>
    [Fact]
    public void Macro_NamespaceIsSeparate() =>
        Parse("""
            macro log match Expression:e expand { print(e); };

            def log = fn(v: Int) Int { return v; };

            @log 1;
            print(log(2));
            """).Statements.Length.ShouldBe(4);

    // -------------------------------------------- delimitação da invocação

    private static MacroInvocation Invocation(string source, int index = 0) =>
        Parse(source).Statements[index]
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<MacroInvocation>();

    [Fact]
    public void Invocation_EndsAtSemicolon()
    {
        var invocation = Invocation("@log value;");

        invocation.Name.ShouldBe("log");
        invocation.Arguments.Select(t => t.Text).ShouldBe(["value"]);
    }

    [Fact]
    public void Invocation_EndsAtBlock()
    {
        var invocation = Invocation("@unless c { a(); }");

        invocation.Arguments[0].Text.ShouldBe("c");
        invocation.Arguments[^1].Kind.ShouldBe(TokenKind.CloseBrace);
    }

    /// <summary>A contagem de aninhamento impede que a chave interna encerre a invocação.</summary>
    [Fact]
    public void Invocation_CountsNesting() =>
        Invocation("@foo { { } }").Arguments.Length.ShouldBe(4);

    /// <summary>Dentro de uma chamada, a vírgula encerra: `f(@foo a, b)` tem dois argumentos.</summary>
    [Fact]
    public void Invocation_StopsAtCommaInsideACall()
    {
        var call = Parse("f(@foo a, b);").Statements[0]
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<CallExpression>();

        call.Arguments.Length.ShouldBe(2);
        call.Arguments[0].ShouldBeOfType<MacroInvocation>().Arguments.Select(t => t.Text).ShouldBe(["a"]);
    }

    [Fact]
    public void Invocation_InExpressionPosition() =>
        Parse("def r = @square 3;").Statements[0]
            .ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<MacroInvocation>()
            .Name.ShouldBe("square");

    /// <summary>Terminada em bloco, a invocação dispensa `;` — como `if p { }`.</summary>
    [Fact]
    public void Invocation_WithBlock_NeedsNoSemicolon() =>
        Parse("@unless c { a(); }\nprint(1);").Statements.Length.ShouldBe(2);

    [Fact]
    public void Invocation_RequiresAName() =>
        ParseWithDiagnostics("@ 1;").Diagnostics.Select(d => d.Code)
            .ShouldContain(DiagnosticCodes.ExpectedIdentifier);
}
