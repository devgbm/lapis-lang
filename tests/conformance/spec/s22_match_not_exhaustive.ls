// spec §22 — `match` deve ser exaustivo (Q6): é uma expressão e precisa produzir
// um valor em toda execução.
// expect: error LAP0262
// ---
def Color = enum { Red, Green, Blue };

def f = fn(c: Color) Str {
    match c {
        Color.Red => return "vermelho",
        Color.Green => return "verde"
    }
};
