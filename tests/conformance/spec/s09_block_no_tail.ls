// spec §9 — bloco sem cauda vale `Void`.
// expect: output
// ()
// ---
def x = { def y = 10; };

print(x);
