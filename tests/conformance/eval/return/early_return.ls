// expect: output
// negativo
// ---
def classificar = fn(x: Int) Str {
    if x < 0 {
        return "negativo";
    }

    return "não negativo";
};

print(classificar(-1));
