// spec §15 — enums, com variantes sempre qualificadas (Q3).
// expect: output
// Color.Green
// verde
// ---
def Color = enum { Red, Green, Blue };

def nome = fn(c: Color) Str {
    match c {
        Color.Red => return "vermelho",
        Color.Green => return "verde",
        Color.Blue => return "azul"
    }
};

print(Color.Green);
print(nome(Color.Green));
