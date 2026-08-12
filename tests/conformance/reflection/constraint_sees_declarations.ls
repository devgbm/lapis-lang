// Reflection em compile time (plano 19 §19.3).
//
// A fonte aqui é **sintática**: o type checker ainda não rodou sobre o programa,
// então `typeName` é o tipo *como escrito*. Para o que uma constraint precisa —
// nomes de campos, de variantes e aridade — isso basta.
//
// A macro valida uma forma: recusa um tipo sem variantes. É o uso inteiro de
// reflection em compile time — ela não recebeu o enum, recebeu o **nome** dele, e
// é `reflect` que transforma um no outro.
// expect: output
// Color tem variantes
// ---
def Color = enum {
    Red,
    Green,
    Blue
};

def User = type {
    id: Int;
};

macro exige_variantes
    match Identifier:nome

    constraint {
        if match reflect(nome).variants[0] {
            Option.Some(primeira) => false,
            Option.None => true
        } {
            throw "esperado um enum com pelo menos uma variante";
        }
    }

    expand { print("Color tem variantes"); };

@exige_variantes Color;
