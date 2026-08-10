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
