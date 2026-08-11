// Um `expand` que não produz valor não serve em posição de expressão.
// expect: error LAP0506 at 7:9
macro noop
    match Expression:e
    expand { print(e); };

def r = @noop 1;
