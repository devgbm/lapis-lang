// spec §12 — `abs` com retorno antecipado. Depende de Q16: um `if` usado como
// statement dispensa `;`.
// expect: output
// 5
// 5
// ---
def abs = fn(x: Int) Int {
    if x < 0 {
        return -x;
    }

    return x;
};

print(abs(5));
print(abs(-5));
