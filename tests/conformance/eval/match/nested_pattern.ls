// expect: output
// 5
// ---
def Inner = enum { Value(Int) };
def Outer = enum { Wrap(Inner) };

def f = fn(o: Outer) Int {
    match o {
        Outer.Wrap(Inner.Value(n)) => return n
    }
};

print(f(Outer.Wrap(Inner.Value(5))));
