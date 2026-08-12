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
// Escopo, que é onde a implementação de um laço por macro costuma vazar:
//
//   - o corpo é um escopo, como o de um `if` — o que ele declara não escapa;
//   - um `var` declarado entre dois laços é visível no segundo;
//   - o que um `goto` explícito pode ter pulado continua invisível no destino,
//     que é a única forma de o contrário ser mentira.
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

@while i < 3 {
    i = i + 1;
    print(i);
}

// Cada laço declara o seu próprio estado, logo antes de si: os rótulos que a
// macro introduz abrem um grupo aninhado, não um irmão do anterior.
var n = 1;
var soma = 0;

@while n <= 10 {
    // `parcial` é do corpo, e não existe depois do laço.
    def parcial = soma + n;

    soma = parcial;
    n = n + 1;
}

print(soma);

var k = 0;

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
