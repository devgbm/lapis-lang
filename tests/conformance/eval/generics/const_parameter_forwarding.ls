// Q18: um parâmetro const é constante e pode ser repassado adiante.
// expect: output
// 30
// ---
def scale = fn<N: Int>(x: Int) Int { return x * N; };

def twice = fn<M: Int>(x: Int) Int {
    return scale<M>(x) + scale<M>(x);
};

print(twice<3>(5));
