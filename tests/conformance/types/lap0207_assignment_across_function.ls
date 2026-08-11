// Ler já é erro; escrever, idem.
// expect: error LAP0207 at 4:21
var x = 1;
def f = fn() Void { x = 2; };
f();
