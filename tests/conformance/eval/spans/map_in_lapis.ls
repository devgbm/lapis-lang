// `map` escrito **em LapisLang** — o critério de aceitação da fase A do roteiro
// 0.3, e a razão pela qual A1 (recursão), A2 (`length`) e A3 (`xs[i] = v`)
// existem.
//
// Antes das três, nenhuma expressão produzia um span de elementos computados e
// distintos: havia a lista literal e a repetição, e mais nada. O que a Q28 supôs
// resolvido "por API" não tinha como existir, porque a API também precisaria
// destas primitivas.
// expect: output
// [10, 20, 30]
// [1, 4, 9]
// []
// ---
def map = fn(xs: [Int;?], f: fn(Int) Int) [Int;?] {
    var out = .[Int; 0; xs.length];
    var i = 0;

    loop {
        if i >= xs.length { break; }

        if xs[i] is Some(v) { out[i] = f(v); }

        i = i + 1;
    }

    return out;
};

print(map(.[1, 2, 3], fn(x: Int) Int { return x * 10; }));
print(map(.[1, 2, 3], fn(x: Int) Int { return x * x; }));

def vazio: [Int;?] = .[Int; 0; 0];
print(map(vazio, fn(x: Int) Int { return x; }));
