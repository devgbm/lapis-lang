// `mutavel.campo = e;` — a mutabilidade segue o **binding**, não a forma do alvo
// (plano 21 §21.3b). Num `def` a mesma escrita é `LAP0206`.
//
// A semântica é atualização funcional: o struct é reconstruído e o slot recebe o
// valor novo. Nada do que a Q25 comprou se perde — todo `Value` continua
// imutável, a única coisa mutável continua sendo o slot, e não há aliasing.
//
// O preço é semântica de **valor**, e ele é observável: `def copia = u;` guarda o
// valor de então, não uma referência. Está escrito aqui porque é comportamento,
// não detalhe de implementação.
// expect: output
// User { name: "depois", age: 1 }
// antes
// Rua B
// ---
def Endereco = type { rua: Str; };
def User = type { name: Str; age: Int; };
def Pessoa = type { endereco: Endereco; };

var u = .User { name: "antes", age: 1 };

def copia = u;

u.name = "depois";

print(u);
print(copia.name);

// Aninhado sai de graça da mesma forma: cada nível reconstrói o seu struct.
var p = .Pessoa { endereco: .Endereco { rua: "Rua A" } };

p.endereco.rua = "Rua B";

print(p.endereco.rua);
