// O ramo morto some do residual, e o `1 / 0` lá dentro nunca chega a ser
// avaliado — era inalcançável no original também.
// expect: output
// 1
// vivo
// ---
def x = if false { 1 / 0 } else { 1 };
print(x);

if true {
    print("vivo");
} else {
    print("morto");
}
