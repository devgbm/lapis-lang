// spec §13 — o exemplo central de const generics, com os cinco tipos de
// argumento: Str, Int, Bool, tipo e função literal.
// expect: output
// <tipo SomeType>
// ---
def SomeType = type<Label: Str, Count: Int, Enabled: Bool, T, Make: fn() Int> {
    label: Str;
};

def t = SomeType<"value", 1, true, Int, fn() Int { return 1; }>;

print(t);
