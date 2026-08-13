// O literal fecha o circuito que a indexação tinha aberto: antes dele, um `Char`
// entrava na linguagem por `s[i]` e não havia como escrever o caractere de
// referência com que compará-lo.
// expect: output
// 3
// ---
def contaVogais = fn(texto: Str) Int {
    var vistas = 0;
    var i = 0;

    loop {
        if i >= texto.length { break; }

        if texto[i] is Some(c) {
            if c == 'a' { vistas = vistas + 1; }
            if c == 'e' { vistas = vistas + 1; }
        }

        i = i + 1;
    }

    return vistas;
};

print(contaVogais("caneta"));
