// `is` — testar variante e desembrulhar carga (plano 25, fecha Q23).
//
//     lapis run    examples/is.ls
//     lapis expand examples/is.ls    # is some ainda aparece — vira match no desugar, não na expansão
//
// `match` continua a única forma completa — mas para o caso comum, uma
// variante só, ele é cerimônia demais:
//
//     match result { Option.Some(value) => return value, Option.None => return fallback }
//     if result is Some(value) { return value } else { return fallback }
//
// As duas formas produzem exatamente a mesma Core (§25.2): `is` é açúcar, não
// primitiva nova.
//
// A ligação só existe onde o desugar consegue lhe dar escopo — condição de
// `if` e operando esquerdo de `&&` (§25.3). Em qualquer outro lugar, `is`
// sem ligação continua valendo: é só um `Bool`.
//
// A variante pode vir sem qualificação (`Some`, não `Option.Some`) — o enum
// sai do tipo do escrutinado, não de o autor repeti-lo. `match` continua
// exigindo a forma qualificada (Q3); é só `is` que relaxa.
//
// Saída esperada:
//   20
//   0
//   true
//   false
//   30

def unwrapOr = fn(result: Option<Int>, fallback: Int) Int {
    if result is Some(value) {
        return value;
    }

    return fallback;
};

var values = .[10, 20, 30];

print(unwrapOr(values[1], 0));
print(unwrapOr(values[9], 0));

// Sem ligação, `is` é só um teste de etiqueta.
def segundo = values[1];

print(segundo is Some);
print(values[9] is Some);

// E a composição com `&&`: a ligação atravessa para o operando direito.
def terceiro = values[2];

if terceiro is Some(value) && value > 20 {
    print(value);
}
