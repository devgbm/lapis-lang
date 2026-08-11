using System.Collections.Immutable;
using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.Cli.Tests;

public abstract class ConstraintTestBase
{
    protected static CompilationResult Compile(string source) =>
        Pipeline.Compile(SourceText.From(source), PipelineStage.Evaluate);

    protected static ImmutableArray<string> Codes(string source) =>
        [.. Compile(source).Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.Code)];

    protected static string Output(string source)
    {
        var output = new StringOutput();
        var result = Pipeline.Compile(SourceText.From(source), PipelineStage.Evaluate, new RuntimeContext(output));

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty(
            $"esperado compilar, obtido: {string.Join(", ", result.Diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        return output.Text;
    }

    /// <summary>
    /// O que as <c>constraint</c> imprimiram. É saída do <b>compilador</b>, e por
    /// isso não se mistura à do programa — no CLI ela vai para <c>stderr</c>,
    /// junto dos diagnósticos.
    /// </summary>
    protected static string CompileTimeOutput(string source)
    {
        var compileTime = new StringOutput();

        var result = Pipeline.Compile(
            SourceText.From(source),
            PipelineStage.Evaluate,
            new RuntimeContext(new StringOutput()),
            compileTimeOutput: compileTime);

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty(
            $"esperado compilar, obtido: {string.Join(", ", result.Diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        return compileTime.Text;
    }

    protected static Diagnostic ShouldFailWith(string source, string code)
    {
        var errors = Compile(source).Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        errors.ShouldContain(
            d => d.Code == code,
            $"esperado {code}, obtido: {string.Join(", ", errors.Select(d => $"{d.Code} {d.Message}"))}");

        return errors.First(d => d.Code == code);
    }
}

/// <summary>
/// <c>constraint</c> rodando entre o <c>match</c> e o <c>expand</c>
/// (plano 18 §18.5, spec de macros §8).
/// </summary>
public sealed class ConstraintTests : ConstraintTestBase
{
    [Fact]
    public void Constraint_Passing_Expands() =>
        Output("""
            macro checked
                match Str:s
                constraint { def ok = s; }
                expand { print(s); };

            @checked "oi";
            """).ShouldBe("oi\n");

    [Fact]
    public void Constraint_Throwing_FailsCompilation()
    {
        var diagnostic = ShouldFailWith(
            """
            macro rejeita
                match Str:s
                constraint { throw "não"; }
                expand { print(s); };

            @rejeita "oi";
            """,
            DiagnosticCodes.ConstraintRejected);

        diagnostic.Message.ShouldBe("não");
    }

    /// <summary>
    /// O span é o da <b>invocação</b>: quem escreveu <c>@rejeita "oi"</c> precisa
    /// ver a sua linha, não a da macro (plano 18 §18.6).
    /// </summary>
    [Fact]
    public void Constraint_Rejection_PointsAtTheInvocation()
    {
        const string Source = """
            macro rejeita
                match Str:s
                constraint { throw "não"; }
                expand { print(s); };

            @rejeita "oi";
            """;

        var diagnostic = ShouldFailWith(Source, DiagnosticCodes.ConstraintRejected);

        Source[diagnostic.Span.Start..diagnostic.Span.End].ShouldBe("@rejeita \"oi\"");
        diagnostic.Notes.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Uma constraint que rejeita <b>não</b> faz o mecanismo tentar outra regra:
    /// falha de <c>match</c> quer dizer "não é esta a forma", falha de
    /// <c>constraint</c>, "esta forma está errada" (spec §7.1).
    ///
    /// O que se observa aqui é o único jeito de observar isso: a segunda regra
    /// existe, não casa, e o erro é o da primeira. Um caso em que <b>as duas</b>
    /// casassem não é construível — a spec §7 manda testar todas as regras e
    /// exigir exatamente uma, então "tentar a próxima" nem chega a ser uma
    /// alternativa; duas regras que casam já são <c>LAP0502</c>.
    /// </summary>
    [Fact]
    public void Constraint_DoesNotTryNextRule() =>
        Codes("""
            macro dois
                match Str:s constraint { throw "primeira"; } expand { print(s); }
                match Int:n expand { print(n); };

            @dois "oi";
            """).ShouldBe([DiagnosticCodes.ConstraintRejected]);

    /// <summary>A constraint roda <b>depois</b> do match: os bindings estão visíveis nela.</summary>
    [Fact]
    public void Constraint_RunsAfterMatch()
    {
        var diagnostic = ShouldFailWith(
            """
            macro eco
                match Str:s
                constraint { throw "recebi " + s; }
                expand { print(s); };

            @eco "/produtos";
            """,
            DiagnosticCodes.ConstraintRejected);

        diagnostic.Message.ShouldBe("recebi /produtos");
    }

    /// <summary>E <b>antes</b> do expand: rejeitada, nenhum nó é produzido.</summary>
    [Fact]
    public void Constraint_RunsBeforeExpand()
    {
        var result = Compile("""
            macro rejeita
                match Str:s
                constraint { throw "não"; }
                expand { print("expandiu"); };

            @rejeita "oi";
            """);

        result.Evaluation.ShouldBeNull();
    }

    /// <summary>
    /// Só capturas de literal são visíveis por enquanto. Um <c>Expression:e</c>
    /// capturou uma <b>árvore</b>, e lê-la como valor é reflection — plano 19.
    /// Até lá o nome não existe, e o erro é o <c>LAP0201</c> normal.
    /// </summary>
    [Fact]
    public void Constraint_SeesLiteralCaptures_ButNotTrees()
    {
        Output("""
            macro lit
                match Int:n
                constraint { def dobro = n * 2; }
                expand { print(n); };

            @lit 21;
            """).ShouldBe("21\n");

        Codes("""
            macro arvore
                match Expression:e
                constraint { def copia = e; }
                expand { print(e); };

            @arvore 1 + 1;
            """).ShouldContain(DiagnosticCodes.UnknownVariable);
    }

    /// <summary>
    /// Uma constraint que não compila reporta o erro <b>dela</b>, e só ele: somar
    /// um <c>LAP0503</c> por cima esconderia a causa.
    /// </summary>
    [Fact]
    public void Constraint_ThatDoesNotCompile_ReportsOnlyItsOwnError() =>
        Codes("""
            macro quebrada
                match Str:s
                constraint { def x = 1 + "a"; }
                expand { print(s); };

            @quebrada "oi";
            """).ShouldNotContain(DiagnosticCodes.ConstraintRejected);

    /// <summary>Uma macro sem <c>constraint</c> não paga nada por esta fase existir.</summary>
    [Fact]
    public void NoConstraint_StillExpands() =>
        Output("macro log match Expression:e expand { print(e); };\n@log 1 + 1;")
            .ShouldBe("2\n");
}

/// <summary><c>throw</c> (plano 18 §18.4).</summary>
public sealed class ThrowTests : ConstraintTestBase
{
    /// <summary>
    /// <c>throw</c> tem tipo <c>Never</c>: cabe em posição de valor, como
    /// <c>return</c> (Q13). Se não tipasse, o <c>def</c> abaixo seria erro de tipo
    /// em vez de rejeição.
    /// </summary>
    [Fact]
    public void Throw_IsNever() =>
        ShouldFailWith(
            """
            macro m
                match Str:s
                constraint { def x: Int = throw "não"; }
                expand { print(s); };

            @m "oi";
            """,
            DiagnosticCodes.ConstraintRejected).Message.ShouldBe("não");

    [Fact]
    public void Throw_OutsideConstraint() =>
        Codes("throw \"não\";").ShouldBe([DiagnosticCodes.ThrowOutsideConstraint]);

    [Fact]
    public void Throw_InsideAFunction_IsStillOutsideConstraint() =>
        Codes("def f = fn() Int { throw \"não\"; };\nprint(f());")
            .ShouldContain(DiagnosticCodes.ThrowOutsideConstraint);

    [Fact]
    public void Throw_NonString() =>
        Codes("""
            macro m
                match Str:s
                constraint { throw 1; }
                expand { print(s); };

            @m "oi";
            """).ShouldBe([DiagnosticCodes.ThrowExpectsStr]);

    [Fact]
    public void Throw_MessageReachesDiagnostic() =>
        ShouldFailWith(
            """
            macro m
                match Str:s
                constraint { throw "mensagem exata"; }
                expand { print(s); };

            @m "oi";
            """,
            DiagnosticCodes.ConstraintRejected).Message.ShouldBe("mensagem exata");

    /// <summary>
    /// <c>throw</c> num caminho não tomado não rejeita nada: ele é uma expressão
    /// avaliada, não uma declaração.
    /// </summary>
    [Fact]
    public void Throw_NotTaken_DoesNotReject() =>
        Output("""
            macro m
                match Str:s
                constraint { if false { throw "não"; } }
                expand { print(s); };

            @m "oi";
            """).ShouldBe("oi\n");
}

/// <summary>O contexto de compilação (plano 18 §18.3).</summary>
public sealed class CompileContextTests : ConstraintTestBase
{
    private const string Post = """
        macro post
            match Str:path Block:handler

            constraint {
                def key = "route.POST." + path;

                if contextHas(key) {
                    throw "rota POST já registrada: " + path;
                }

                contextPut(key, path);
            }

            expand {
                print(path);
                handler;
            };

        """;

    [Fact]
    public void Context_PutThenHas() =>
        Output("""
            macro registra
                match Str:s
                constraint {
                    contextPut("k", s);

                    if contextHas("k") {
                        contextPut("visto", "sim");
                    }
                }
                expand { print(s); };

            @registra "a";
            """).ShouldBe("a\n");

    /// <summary>Chave ausente é <c>Err</c>, não aborto: falha esperada aparece no tipo (spec §30).</summary>
    [Fact]
    public void Context_GetMissing_IsErr() =>
        CompileTimeOutput("""
            macro le
                match Str:s
                constraint {
                    print(match contextGet("nunca-escrita") {
                        Result.Ok(v) => "achou",
                        Result.Err(e) => "faltou"
                    });
                }
                expand { print(s); };

            @le "x";
            """).ShouldBe("faltou\n");

    [Fact]
    public void Context_Get_ReturnsWhatWasPut() =>
        CompileTimeOutput("""
            macro roundtrip
                match Str:s
                constraint {
                    contextPut("k", s);

                    print(match contextGet("k") {
                        Result.Ok(v) => v,
                        Result.Err(e) => "faltou"
                    });
                }
                expand { print("fim"); };

            @roundtrip "valor";
            """).ShouldBe("valor\n");

    /// <summary>
    /// E o que a constraint imprime <b>não</b> entra na saída do programa: um
    /// <c>lapis run</c> não pode ter a sua saída mudada por quantas macros o
    /// arquivo usa.
    /// </summary>
    [Fact]
    public void CompileTimePrint_DoesNotReachProgramOutput() =>
        Output("""
            macro ruidosa
                match Str:s
                constraint { print("compilando"); }
                expand { print(s); };

            @ruidosa "oi";
            """).ShouldBe("oi\n");

    [Fact]
    public void Context_Keys_FiltersByPrefix()
    {
        var result = Compile(Post + "@post \"/a\" { print(1); }\n@post \"/b\" { print(2); }");

        result.CompileContext.ShouldNotBeNull();
        result.CompileContext.Keys("route.POST.").ShouldBe(["route.POST./a", "route.POST./b"]);
        result.CompileContext.Keys("outro.").ShouldBeEmpty();
    }

    /// <summary>Duas compilações não se enxergam: o contexto nasce e morre com uma.</summary>
    [Fact]
    public void Context_IsolatedPerCompilation()
    {
        const string Source = Post + "@post \"/produtos\" { print(1); }";

        Codes(Source).ShouldBeEmpty();
        Codes(Source).ShouldBeEmpty();
    }

    /// <summary>O exemplo canônico da spec de macros §8.3.</summary>
    [Fact]
    public void Post_DuplicateRoute_IsRejected() =>
        ShouldFailWith(
            Post + "@post \"/produtos\" { print(1); }\n@post \"/produtos\" { print(2); }",
            DiagnosticCodes.ConstraintRejected)
            .Message.ShouldBe("rota POST já registrada: /produtos");

    [Fact]
    public void Post_DistinctRoutes_Compile() =>
        Output(Post + "@post \"/a\" { print(1); }\n@post \"/b\" { print(2); }")
            .ShouldBe("/a\n1\n/b\n2\n");
}

/// <summary>
/// O isolamento entre as fases — o teste que garante a separação de §21 da
/// proposta de macros.
/// </summary>
public sealed class CompileTimeIsolationTests : ConstraintTestBase
{
    /// <summary>
    /// Uma constraint não executa código da aplicação: as funções do programa não
    /// estão no escopo dela. O <c>def</c> abaixo existe no arquivo e mesmo assim é
    /// nome livre lá dentro.
    /// </summary>
    [Fact]
    public void CompileTime_CannotCallProgramFunctions() =>
        Codes("""
            def dobro = fn(x: Int) Int { return x * 2; };

            macro m
                match Int:n
                constraint { def y = dobro(n); }
                expand { print(n); };

            @m 1;
            """).ShouldContain(DiagnosticCodes.UnknownVariable);

    /// <summary>
    /// Nenhuma nativa de contexto existe fora da compilação. Percorrer
    /// <see cref="CompileTimeNatives.AllNames"/> em vez de listar os nomes aqui é
    /// o que faz uma nativa nova entrar neste teste sem ninguém lembrar dela.
    /// </summary>
    [Fact]
    public void Context_DoesNotLeakToRuntime()
    {
        CompileTimeNatives.AllNames.ShouldNotBeEmpty();

        foreach (var native in CompileTimeNatives.AllNames)
        {
            Codes($"print({native}(\"k\"));").ShouldContain(
                DiagnosticCodes.UnknownVariable, $"'{native}' está visível em runtime");
        }
    }

    /// <summary>E a lista não mente: é a mesma coisa que entra no escopo.</summary>
    [Fact]
    public void AllNames_MatchesTheBindingsActuallyDeclared() =>
        new CompileTimeScope(new CompileContext(), PreludeLoader.Load())
            .Bindings.Select(b => b.Name)
            .ShouldBe(CompileTimeNatives.AllNames, ignoreOrder: true);

    /// <summary>
    /// E o programa expandido não vê o contexto nem indiretamente: o que a macro
    /// registrou fica no compilador, e o que roda é só a sintaxe que o
    /// <c>expand</c> produziu.
    /// </summary>
    [Fact]
    public void Constraint_HasNoSideEffectOnProgram()
    {
        var result = Compile("""
            macro registra
                match Str:s
                constraint { contextPut("segredo", s); }
                expand { print(s); };

            @registra "a";
            """);

        result.CompileContext!.Has("segredo").ShouldBeTrue();
        result.Surface!.ToString().ShouldNotContain("segredo");
    }

    /// <summary>
    /// O evaluator é <b>o mesmo</b> nas duas fases (plano 18, critério final).
    /// Se houvesse um fork, o dia em que um deles ganhasse uma regra a mais o
    /// outro não a teria — e é isso que este teste impede.
    /// </summary>
    [Fact]
    public void SameEvaluator_InBothPhases() =>
        typeof(Evaluator.Evaluator)
            .GetMethods()
            .Count(m => m.Name == nameof(Evaluator.Evaluator.Run))
            .ShouldBe(1);
}
