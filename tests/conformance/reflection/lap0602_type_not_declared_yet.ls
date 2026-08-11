// Dentro de um `constraint`, `reflect` enxerga só o que foi declarado **acima** da
// invocação — a mesma regra de `def` (Q8). `Depois` existe no arquivo, e mesmo
// assim não está lá.
// expect: error LAP0602 at 7:32
macro descreve
    match Str:s
    constraint { print(reflect(Depois).name); }
    expand { print(s); };

@descreve "oi";

def Depois = type {
    x: Int;
};
