// expect: output
// 11
// 12
// ---
def makeAdder = fn(n: Int) fn(Int) Int {
    return fn(x: Int) Int { return x + n; };
};

def add1 = makeAdder(1);
def add2 = makeAdder(2);

print(add1(10));
print(add2(10));
