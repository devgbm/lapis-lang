// spec §12/§26 — função não-Void precisa retornar em todos os caminhos.
// expect: error LAP0272
// ---
def f = fn(c: Bool) Int {
    if c {
        return 1;
    }
};
