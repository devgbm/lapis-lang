// Tipos definidos pelo usuário e pattern matching (spec §14, §15, §22).
//
// A construção leva ponto inicial — `.User { ... }` — o que permite distinguir
// bloco de construção com um único token (decisão Q2). Por isso a construção é
// válida até na condição de um `if`, sem parênteses.
//
// Saída esperada:
//   User { id: 1, name: "Gabriel" }
//   Gabriel
//   ativo
//   verde

def User = type {
    id: Int;
    name: Str;
    active: Bool;
};

def user = .User {
    id: 1,
    name: "Gabriel",
    active: true
};

print(user);
print(user.name);

if user.active {
    print("ativo");
}

def Color = enum {
    Red,
    Green,
    Blue
};

def nome = fn(c: Color) Str {
    match c {
        Color.Red => return "vermelho",
        Color.Green => return "verde",
        Color.Blue => return "azul"
    }
};

print(nome(Color.Green));
