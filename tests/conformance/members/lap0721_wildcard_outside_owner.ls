// `?` **não** é um tipo. Ele existe só na posição de dono de um membro, onde
// significa "esta declaração vale para qualquer coisa aqui".
//
// Permiti-lo como tipo de valor seria um `Any` estrutural pela porta dos fundos:
// `c: Par<?, ?>` prometeria um `Par` sobre o qual nada se pode fazer, e aceitaria
// qualquer um.
// expect: error LAP0721 at 11:19
// expect: error LAP0721 at 11:22
def Par = type<A, B> { a: A; b: B; };

def f = fn(c: Par<?, ?>) Bool { return true; };
