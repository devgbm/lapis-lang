// `def Result<?, ?>.isOk` — membros sobre tipos genéricos (plano 23).
//
// `?` é **curinga**, não parâmetro: ele não liga nome nenhum. É isso que dissolve
// a tensão da Q27 com a Q7 (argumento genérico nunca é inferido) — casar
// `Result<Int, Str>` contra `Result<?, ?>` não é unificação, é comparação posição
// a posição.
//
// A leitura é a mesma de `[Int;?]`: "não se diz o que vai aqui". E a regra de
// atribuibilidade é a mesma, na mesma direção — `Result<Int, Str>` cabe onde se
// espera `Result<?, ?>`, e não o contrário. Esquecer o que se sabia é seguro;
// afirmar o que não se sabe, não.
//
// O alcance de cada declaração está **escrito**. Dois padrões disjuntos convivem
// e o receptor escolhe; dois que se cruzam são LAP0720.
// expect: output
// true
// int
// bool
// 7
// Res.Ok(10)
// ---
def Par = type<A, B> {
    a: A;
    b: B;
};

// Vale para qualquer Par.
def Par<?, ?>.cheio = fn(self) Bool {
    return true;
};

// Padrões disjuntos: quem escolhe é o tipo do receptor.
def Par<Int, ?>.qual = fn(self) Str {
    return "int";
};

def Par<Bool, ?>.qual = fn(self) Str {
    return "bool";
};

// Sobre `Par<Int, ?>` o campo `a` é legível: o tipo dele não depende do curinga.
def Par<Int, ?>.primeiro = fn(self) Int {
    return self.a;
};

def pi = .Par<Int, Str> { a: 7, b: "s" };
def pb = .Par<Bool, Str> { a: true, b: "s" };

print(pi.cheio());
print(pi.qual());
print(pb.qual());
print(pi.primeiro());

// O outro lado da regra: `<>` à direita do `=` fala do **membro**, não do dono.
// Isto não precisou de código novo — é a cadeia pós-fixa do M4.
def Res = enum<T, E> {
    Ok(T),
    Err(E)
};

def Res.ok = fn<T>(v: T) Res<T, Str> {
    return Res<T, Str>.Ok(v);
};

print(Res.ok<Int>(10));
