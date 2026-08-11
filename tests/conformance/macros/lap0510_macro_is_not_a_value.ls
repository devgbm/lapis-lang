// Q19: uma macro não é first-class citizen — não existe em runtime, não é
// argumento, não é retorno. `def` liga valores, e macro não é um.
// expect: error LAP0102 at 5:1
// expect: error LAP0510 at 5:9
def m = macro n match Expression:e expand { e };
