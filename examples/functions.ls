// Funções são valores de primeira classe (spec §10, §11) e closures (spec §32).
// Saída esperada:
//   6
//   50
//   5

def apply = fn(f: fn(Int) Int, value: Int) Int {
    return f(value);
};

def increment = fn(x: Int) Int {
    return x + 1;
};

print(apply(increment, 5));

// Captura de ambiente (spec §32).
def multiplier = 10;

def multiply = fn(x: Int) Int {
    return x * multiplier;
};

print(multiply(5));

// Retorno antecipado (spec §12).
def abs = fn(x: Int) Int {
    if x < 0 {
        return -x;
    }

    return x;
};

print(abs(-5));
