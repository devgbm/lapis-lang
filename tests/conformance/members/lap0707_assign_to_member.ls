// `hello` **existe** em `User`, só não é campo da instância. `LAP0250` ("campo
// desconhecido") seria mentira, e é por isso que este código existe.
//
// Redefinir um membro por instância não é permitido: o membro é do tipo, e o que
// uma instância carrega são os campos.
// expect: error LAP0707 at 13:3
def User = type { name: Str; };

def User.hello = fn() Void { };

var u = .User { name: "a" };

u.hello = fn() Void { };
