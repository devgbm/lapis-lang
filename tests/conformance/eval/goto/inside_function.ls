// Um salto é local à função e não vaza dela: o valor devolvido é o normal.
// expect: output
// 42
// ---
def escolher = fn(usar: Bool) Int {
    goto padrao if usar;
    return 0;
    label padrao;
    return 42;
};

print(escolher(true));
