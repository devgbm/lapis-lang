// Um membro homônimo de variante tornaria `Color.Red` ambíguo, e a ambiguidade
// seria resolvida por precedência silenciosa — que é o que este projeto evita.
//
// Recusar é mais barato do que escolher: o programa continua dizendo exatamente
// uma coisa em cada lugar.
// expect: error LAP0703 at 9:5
def Color = enum { Red, Green };

def Color.Red = 1;
