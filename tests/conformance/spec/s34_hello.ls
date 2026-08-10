// spec §34 — a âncora do M1: a saída é apenas `30`. O valor final do programa
// não é impresso; a saída vem exclusivamente de `print`.
// expect: output
// 30
// ---
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

def main = fn() Void {
    def result = add(10, 20);

    print(result);
};

main();
