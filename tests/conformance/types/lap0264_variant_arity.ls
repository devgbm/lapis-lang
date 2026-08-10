// LAP0264 — aridade errada no padrão de variante. O braço ainda conta como
// coberto: um erro de aridade não deve arrastar um LAP0262 junto.
// expect: error LAP0264
// ---
def Wrap = enum { One(Int) };

def f = fn(w: Wrap) Int {
    match w {
        Wrap.One(a, b) => return 1
    }
};
