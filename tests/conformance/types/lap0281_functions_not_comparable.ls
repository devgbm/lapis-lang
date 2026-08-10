// expect: error LAP0281
// ---
def f = fn() Int { return 1; };
def g = fn() Int { return 2; };
def x = f == g;
