// `is` testa **uma** variante e não é exaustivo — o resto cai no ramo falso, e
// nenhum LAP0262 é exigido. `match` continua sendo o exaustivo (Q6).
//
// A divisa não é acidente: é ela que permite a `match` um dia sair do
// compilador e virar `@match` sobre esta primitiva, quando o sistema de macros
// souber provar exaustividade (Q22, plano 25).
// expect: output
// 1
// sem valor
// ---
def a = Option<Int>.Some(1);
def b: Option<Int> = Option<Int>.None;

// Sem `else`, e sem cobrir `None`: aceito.
if a is Some(v) {
    print(v);
}

if b is Some(v) {
    print(v);
} else {
    print("sem valor");
}
