// Um `break` é local ao `loop` que ele alcança e não vaza da função: o valor
// devolvido é o normal.
// expect: output
// 42
// ---
def escolher = fn(usar: Bool) Int {
    loop {
        if usar { break; }
        return 0;
    }
    return 42;
};

print(escolher(true));
