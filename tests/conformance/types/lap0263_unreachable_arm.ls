// LAP0263 é aviso: o braço é inalcançável, mas o programa roda.
// expect: warning LAP0263
// expect: output
// 1
// ---
def x = match 1 {
    _ => 1,
    2 => 2
};

print(x);
