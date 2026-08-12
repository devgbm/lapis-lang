// Option e pattern matching (spec §16, §22).
//
// Variantes de enum exigem qualificação completa: `Option.Some`, não `Some`
// (decisão Q3 em plans/appendix-c-decisions.md).
//
// O escrutinado é a indexação de um span de tamanho desconhecido — o `var` é o
// que apaga o tamanho do tipo. Fosse um `def`, o compilador já teria provado os
// limites e não haveria envelope nenhum para desembrulhar; ver `spans.ls`.
//
// Saída esperada:
//   20
//   0

def unwrapOr = fn(result: Option<Int>, fallback: Int) Int {
    match result {
        Option.Some(value) => return value,
        Option.None => return fallback
    }
};

var values = .[10, 20, 30];

print(unwrapOr(values[1], 0));
print(unwrapOr(values[9], 0));
