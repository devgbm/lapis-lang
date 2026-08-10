// expect: output
// dois
// ---
def f = fn(n: Int) Str {
    match n {
        1 => return "um",
        2 => return "dois",
        _ => return "outro"
    }
};

print(f(2));
