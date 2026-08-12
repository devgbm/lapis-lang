// `self` sem anotação só vale no **primeiro** parâmetro de um `def T.m`, que é a
// única exceção à exigência de anotar parâmetro (spec §26).
//
// Fora dali não há de onde tirar o tipo — e é por isso que a exceção é estreita
// em vez de "um parâmetro chamado `self` é especial em toda parte".
// expect: error LAP0712 at 7:12
def f = fn(self) Int {
    return 1;
};
