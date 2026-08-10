// LAP0113 — braço de match cujo lado esquerdo não é um padrão.
// expect: error LAP0113 at 4:19
def x = 1;
def y = match x { * => 0 };
