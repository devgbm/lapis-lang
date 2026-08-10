// expect: output
// 10
// texto
// 15
// ---
def identity = fn<T>(v: T) T { return v; };
def scale = fn<N: Int>(x: Int) Int { return x * N; };

print(identity<Int>(10));
print(identity<Str>("texto"));
print(scale<3>(5));
