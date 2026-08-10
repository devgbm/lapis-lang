// expect: output
// Point { x: 1, y: 2 }
// 1
// ---
def Point = type { x: Int; y: Int; };

def p = .Point { x: 1, y: 2 };

print(p);
print(p.x);
