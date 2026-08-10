// LAP0111 — posição de tipo com algo que não é tipo. O LAP0103 vem junto: a
// recuperação abandona o `def`, e o `=` some com ele.
// expect: error LAP0111
// expect: error LAP0103
// ---
def x: 1 = 2;
