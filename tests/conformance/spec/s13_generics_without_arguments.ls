// spec §13 — chamada genérica sem argumentos é erro: não há inferência (Q7).
// expect: error LAP0290
// ---
def identity = fn<T>(value: T) T {
    return value;
};

identity(10);
