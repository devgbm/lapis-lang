// Membros de tipo — `def T.m` — e atribuição a campo.
//
//     lapis run examples/type-members.ls
//     lapis pe  examples/type-members.ls    # os membros conhecidos somem
//
// `def User.create` associa um nome a um **tipo**, e não ao escopo. Não há fase
// nova no pipeline nem nó novo na Core: `User.create` já é o acesso a membro que
// existe desde o M2, e o que mudou foi o checker ganhar uma terceira leitura para
// ele — depois de campo de instância e de variante de enum.
//
// O valor vive num `Let` comum sob um nome sintético que não é escrevível em
// fonte, então o evaluator não precisou de caminho nenhum: ele lê um nome.
//
// Saída esperada:
//   30
//   User { name: "Gabriel", age: 30 }
//   3
//   antes
//   depois
//   Rua B

def User = type {
    name: Str;
    age: Int;
};

// Um valor associado ao tipo. Serve de argumento const genérico como qualquer
// outra constante (Q18) — é um `def` ligado a literal, só que com dono.
def User.defaultAge = 30;

// E uma função. Sem `self` ainda: toda função ligada por `def T.m` é estática
// neste milestone; métodos de instância chegam no M14.
def User.create = fn(nome: Str) User {
    return .User { name: nome, age: User.defaultAge };
};

print(User.defaultAge);
print(User.create("Gabriel"));

// Um membro **não** colide com um `def` comum de mesmo nome: são símbolos
// distintos, porque o segundo só é alcançável através do tipo.
def create = 3;

print(create);

// ------------------------------------------------- atribuição a campo

// A mutabilidade segue o **binding**, não a forma do alvo: num `def` a mesma
// escrita é LAP0206.
var u = .User { name: "antes", age: 1 };

// Aqui está a parte que vale ler duas vezes. A atribuição não muda o struct no
// lugar: ela reconstrói o valor e reatribui o slot. Com isso todo valor da
// linguagem continua imutável, a única coisa mutável continua sendo o slot do
// ambiente, e não existe aliasing — que é exatamente o que a Q25 comprou e o que
// o partial evaluator depende de não perder.
//
// O preço é semântica de **valor**, e ele é observável: `copia` guarda o valor de
// agora, não uma referência a `u`.
def copia = u;

u.name = "depois";

print(copia.name);
print(u.name);

// Aninhado sai de graça da mesma forma: cada nível reconstrói o seu struct.
def Endereco = type { rua: Str; };
def Pessoa = type { endereco: Endereco; };

var p = .Pessoa { endereco: .Endereco { rua: "Rua A" } };

p.endereco.rua = "Rua B";

print(p.endereco.rua);
