// spec §22 — `match` é uma expressão.
// expect: output
// um
// ---
def n = 1;

def texto = match n {
    1 => "um",
    2 => "dois",
    _ => "outro"
};

print(texto);
