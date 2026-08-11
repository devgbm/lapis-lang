// Partial evaluation — a pergunta de pesquisa do projeto.
//
// Rode os dois e compare:
//
//     lapis run examples/partial-evaluation.ls
//     lapis pe  examples/partial-evaluation.ls
//     lapis pe  examples/partial-evaluation.ls --dynamic=escala --stats
//
// O `pe` imprime o programa **residual**: o mesmo programa com tudo o que já
// dava para decidir, decidido. A garantia é `evaluate(P) ≡ evaluate(PE(P))` —
// o residual roda igual, sempre (spec §40).
//
// Sem entrada externa na 0.2, todo top-level é estático por construção, e um
// programa fechado tende ao resultado já avaliado. `--dynamic=nome` declara um
// nome como desconhecido e é o que torna o exercício interessante: com `escala`
// dinâmica, a multiplicação sobrevive e o resto continua dobrando.
//
// Saída esperada:
//   30
//   ativo
//   21

def escala = 3;
def base = 10;

// Constant folding: `10 * 3` é decidido em tempo de PE.
print(base * escala);

// Ramo morto: o `else` some do residual porque era inalcançável.
def ligado = true;

if ligado {
    print("ativo");
} else {
    print("inativo");
}

// Propagação através de vários bindings, e dentro do corpo de uma função — sem
// inlining, que é do plano 13: `f` continua sendo chamada, mas o `(1 + 2)` do
// corpo dela já foi dobrado.
def a = 2;
def b = a * 3;

def f = fn(v: Int) Int {
    return (1 + 2) * 5 + v;
};

print(f(b));

// O que o PE **não** faz, de propósito: `print` nunca executa em tempo de
// especialização, nem com argumento conhecido. Executá-lo moveria a saída do
// programa para o tempo de compilação — o que a spec §40 proíbe.
