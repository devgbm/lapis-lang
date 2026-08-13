// `Char` é comparável por igualdade como qualquer primitivo (Q35/A2a) — é o que
// torna a indexação útil sem literal de caractere, que a A2a não introduziu:
// as aspas simples estão reservadas para pseudo-palavras-chave de macro (Q38).
// O caractere de referência vem, então, de outra string.
//
// A comparação é sobre os `Char`, não sobre os `Option<Char>`: comparar o
// envelope não compila hoje, e não por causa de `Char` — `Option<T>` não é
// comparável para T nenhum, porque a checagem olha a definição genérica em vez
// do argumento. É o `is` que desembrulha, e é para isso que ele existe (Q23).
// expect: output
// true
// false
// true
// ---
def a = "abc";
def b = "axc";

def iguais = fn(i: Int) Bool {
    if a[i] is Some(x) {
        if b[i] is Some(y) {
            return x == y;
        }
    }

    return false;
};

print(iguais(0));
print(iguais(1));
print(iguais(2));
