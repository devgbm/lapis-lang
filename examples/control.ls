// Controle de fluxo pelo prelude — `@while` e `@unless`.
//
//     lapis run    examples/control.ls
//     lapis expand examples/control.ls    # veja as macros virarem goto/label
//
// Nenhuma das duas é declarada aqui: elas vêm do `prelude.ls`, escritas na
// própria LapisLang sobre `goto` e `label` (spec de macros §12).
//
// É a tese do sistema de macros em uma frase: **um laço, que em qualquer outra
// linguagem é trabalho de compilador, aqui são seis linhas de biblioteca.** E
// nada foi retirado do compilador para isso — `if` e `match` continuam onde
// estavam —, então um `@while` quebrado não tem como regredir um programa que já
// funcionava.
//
// ------------------------------------------------------------------------
// A REGRA DE ESCRITA QUE VOCÊ PRECISA SABER
//
// **Todo `var` que um laço usa se declara antes do primeiro `@while` do bloco.**
//
// Não é estilo. `@while` expande para `label`, e cada `label` de um bloco abre um
// *join* — e um join não enxerga os bindings declarados em outro, porque um salto
// pode ter pulado a declaração. Um `var` escrito entre dois `@while` fica preso no
// join que o primeiro deixou aberto, e o segundo não o encontra:
//
//     var i = 0;
//     @while i < 2 { i = i + 1; }
//     var k = 0;                      // preso no join que `@while` abriu
//     @while k < 3 { k = k + 1; }     // LAP0201: 'k' não existe
//
// É a mesma regra de escopo que `examples/loops.ls` documenta com `goto` e
// `label` escritos à mão. A diferença é que aqui os rótulos são **invisíveis** —
// quem escreve `@while` não tem por que saber que um `label` foi introduzido —, e
// por isso ela precisa estar escrita em letras grandes.
//
// A ergonomia que falta é *join com parâmetros* (`label L(x: Int);` /
// `goto L(x + 1);`), registrada como evolução possível desde o M6.
// ------------------------------------------------------------------------
//
// Saída esperada:
//   1
//   2
//   3
//   55
//   2
//   4
//   6
//   fim

var i = 0;
var n = 1;
var soma = 0;
var k = 0;

@while i < 3 {
    i = i + 1;
    print(i);
}

// Duas invocações no mesmo bloco convivem: os rótulos que a macro introduz são
// higienizados, então o `top` de uma não é o `top` da outra.
@while n <= 10 {
    soma = soma + n;
    n = n + 1;
}

print(soma);

@while k < 6 {
    k = k + 1;

    // `@unless c { b }` é `if !c { b }` — a macro não inventa semântica, ela
    // produz a sintaxe que alguém escreveria à mão. Aninhada no corpo de um
    // `@while`, funciona: o que ela declara é dela, e o que ela lê é de fora.
    //
    // Imprime só os pares: a divisão inteira trunca, então `k / 2 * 2 == k`
    // exatamente quando `k` é par.
    @unless k / 2 * 2 != k {
        print(k);
    }
}

print("fim");
