// `fn(self, ...)` — método de instância (plano 22).
//
// A regra é estreita de propósito: um primeiro parâmetro chamado `self` e **sem
// anotação**, dentro de um `def T.m`, recebe o tipo `T`. É a única exceção à
// exigência de anotar parâmetro (spec §26), e o gatilho é a ausência de anotação
// — não o nome sozinho.
//
// `self` **não** é palavra reservada: `def self = 1;` continua válido, e um
// parâmetro chamado `self` numa função comum é um parâmetro chamado `self`.
//
// Na chamada, o receptor entra como argumento 0. Não há reescrita da árvore: o
// checker registra o receptor na resolução e o evaluator monta a chamada — o que
// é o que mantém o receptor avaliado **uma vez** (ver
// `instance_receiver_once.ls`).
// expect: output
// Gabriel
// 35
// 3
// ---
def User = type {
    name: Str;
    age: Int;
};

def User.saudar = fn(self) Str {
    return self.name;
};

def User.maisVelho = fn(self, anos: Int) Int {
    return self.age + anos;
};

def User.create = fn(nome: Str) User {
    return .User { name: nome, age: 30 };
};

def u = User.create("Gabriel");

print(u.saudar());
print(u.maisVelho(5));

// `self` como nome comum continua valendo.
def self = 3;

print(self);
