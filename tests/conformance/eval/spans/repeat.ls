// `.[T; inicial; n]` — span por repetição.
//
// A forma por lista não escala: um span de oito zeros não se escreve
// programaticamente com `.[0, 0, ...]`, e a quantidade pode não ser conhecida por
// quem escreve. Os separadores são `;`, os mesmos de `[Int;8]` — a lista usa `,`,
// então um token separa as duas leituras.
//
// O elemento é escrito porque ele nem sempre sai do inicializador:
// `Option<Int>.None` não determina sozinho que o span é de `Option<Int>` (Q7 —
// não há inferência).
//
// Com a quantidade constante, o tamanho entra no tipo como em qualquer literal:
// `zeros` é `[Int;8]`, e indexar por índice literal é total.
// expect: output
// [0, 0, 0, 0, 0, 0, 0, 0]
// 8
// 0
// [Option.None, Option.None, Option.None]
// []
// ---
def zeros = .[Int; 0; 8];

print(zeros);
print(zeros.length);
print(zeros[3]);

def vazias = .[Option<Int>; Option<Int>.None; 3];

print(vazias);

// Quantidade zero é legítima, e não precisa de anotação: o elemento está escrito.
def nenhum = .[Str; "x"; 0];

print(nenhum);
