// Encontrado ao escrever o caso de LAP0264 no M5.
//
// Aridade errada num padrão de variante produzia LAP0264 **e** LAP0262: o braço
// não contava como coberto, então o `match` parecia não exaustivo. Dois
// diagnósticos para um erro só — contra o critério "zero cascatas" do plano 06.
// expect: error LAP0264
// ---
def Wrap = enum { One(Int) };

def f = fn(w: Wrap) Int {
    match w {
        Wrap.One(a, b) => return 1
    }
};
