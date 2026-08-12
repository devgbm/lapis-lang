// Macros — sintaxe → sintaxe, definida pela própria linguagem.
//
//     lapis run    examples/macros.ls
//     lapis expand examples/macros.ls    # a Surface AST depois da expansão
//
// Uma macro é declarada por `macro`, não por `def`: ela não é first-class
// citizen (Q19). Não existe em runtime, não é argumento, não é retorno — e some
// do programa antes do desugar.
//
// A invocação começa com `@`, o que a separa de chamada de função sem
// ambiguidade nenhuma, e não exige parênteses: o que ela consome é determinado
// pelo `match` da própria macro.
//
// Saída esperada:
//   3
//   16
//   executou
//   7
//   meu

macro log
    match Expression:e
    expand { print(e); };

// A captura usa a gramática de expressão normal, que já para nos lugares certos:
// `1 + 2` vem inteiro, porque `+` continua a expressão.
@log 1 + 2;

macro square
    match Expression:e
    expand { (e * e) };

// Os parênteses no `expand` são responsabilidade de quem escreve a macro: a
// árvore capturada entra como árvore, e `(e * e)` já é a certa.
print(@square 2 + 2);

// `@unless` é a macro que o `goto`/`label` do M6 existia para viabilizar — hoje
// construída sobre `if` (plano 26 — Q32). Ela acrescenta uma construção que a
// linguagem não tem, sem tocar no compilador.
macro unless
    match Expression:condition Block:body
    expand {
        if !condition {
            body;
        }
    };

def pular = false;

@unless pular {
    print("executou");
}

// Um literal sintático pertence à macro, não à linguagem: `in` não vira palavra
// reservada, e nenhuma outra construção passa a conhecê-lo.
macro bind
    match Identifier:nome in Expression:valor Block:corpo
    expand {
        def nome = valor;
        corpo;
    };

@bind x in 7 {
    print(x);
}

// Higiene: o `temp` que a macro introduz não é o `temp` do programa. Na expansão
// ele vira `temp@1` — um nome que a linguagem reserva para expansão.
macro example
    match Block:body
    expand {
        def temp = 10;
        body;
    };

def temp = "meu";

@example { print(temp); }
