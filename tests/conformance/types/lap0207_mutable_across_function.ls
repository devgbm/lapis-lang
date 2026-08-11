// Um `var` não atravessa fronteira de função (Q25). É a restrição que dispensa
// decidir se uma closure captura por valor ou por referência: nenhuma captura.
// expect: error LAP0207 at 5:27
var x = 1;
def f = fn() Int { return x; };
print(f());
