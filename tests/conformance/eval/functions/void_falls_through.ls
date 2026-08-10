// Spec §12: uma função Void pode cair no fim do corpo sem `return`.
// expect: output
// ok
// ()
// ---
def f = fn() Void { print("ok"); };

print(f());
