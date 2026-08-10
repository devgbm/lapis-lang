// expect: output
// Wrap.One(7)
// 7
// ---
def Wrap = enum { One(Int), None };

def w = Wrap.One(7);

print(w);

def valor = fn(x: Wrap) Int {
    match x {
        Wrap.One(v) => return v,
        Wrap.None => return 0
    }
};

print(valor(w));
