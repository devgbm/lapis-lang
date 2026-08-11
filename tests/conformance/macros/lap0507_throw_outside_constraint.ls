// `throw` é exclusivo de compile time: a 0.2 não tem exceções de runtime, e Q9
// tornou a divisão total justamente para eliminar caminhos de aborto.
// expect: error LAP0507 at 6:1
def mensagem = "não";

throw mensagem;
