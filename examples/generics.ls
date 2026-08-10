// Generics escritos pelo programador (spec §13).
//
// Argumentos genéricos são SEMPRE explícitos (Q7): não há inferência na 0.2, e
// `identity(10)` é erro de compilação. A forma explícita continua valendo se um
// dia a inferência chegar, então nenhum programa daqui quebra.
//
// Um parâmetro genérico é um tipo (`T`) ou um valor constante (`N: Int`). O
// checker apenas substitui — quem especializa corpos é o partial evaluator.
//
// Saída esperada:
//   10
//   olá
//   Box { value: 7 }
//   7
//   Option.Some(20)
//   20
//   0
//   15
//   50
//   30
//   [9]

def identity = fn<T>(value: T) T {
    return value;
};

print(identity<Int>(10));
print(identity<Str>("olá"));

// Um `type` genérico: a construção fornece os argumentos junto com o ponto.

def Box = type<T> {
    value: T;
};

def caixa = .Box<Int> { value: 7 };

print(caixa);
print(caixa.value);

// Um `enum` genérico. A variante é sempre qualificada (Q3), e o enum genérico
// precisa dos argumentos antes do ponto: `Option<Int>.Some`, nunca `Option.Some`
// — sem inferência não haveria de onde tirar `T`.

def Option = enum<T> {
    Some(T),
    None
};

def unwrapOr = fn(o: Option<Int>, fallback: Int) Int {
    match o {
        Option.Some(value) => return value,
        Option.None => return fallback
    }
};

print(Option<Int>.Some(20));
print(unwrapOr(Option<Int>.Some(20), 0));
print(unwrapOr(Option<Int>.None, 0));

// Const generics: dentro do corpo, `N` é um valor `Int` como qualquer outro.
// É este o caso interessante para partial evaluation — `N` é conhecido no ponto
// da instanciação, mesmo que `x` só apareça em execução.

def scale = fn<N: Int>(x: Int) Int {
    return x * N;
};

print(scale<3>(5));
print(scale<10>(5));

// Um argumento const precisa ser resolvível em tempo de compilação, e só isso:
// um literal, um `def` ligado a um literal, ou o parâmetro const de um genérico
// envolvente. `N` dentro de `fn<N: Int>` é constante — a regra garante que ele só
// pode ter recebido um valor conhecido — então repassá-lo adiante é válido.

def twice = fn<M: Int>(x: Int) Int {
    return scale<M>(x) + scale<M>(x);
};

print(twice<3>(5));

// O valor de `N` só é conhecido na instanciação de fora, e até lá ele é uma
// constante *simbólica*: `Boxed<Int, N>` é um tipo tão legítimo quanto `Box<T>`,
// e o retorno de `make<4>` é `Boxed<Int, 4>`.

def Boxed = type<T, N: Int> {
    values: T[];
};

def make = fn<N: Int>(v: Int) Boxed<Int, N> {
    return .Boxed<Int, N> { values: [v] };
};

def quatro: Boxed<Int, 4> = make<4>(9);

print(quatro.values);
