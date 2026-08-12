// `def T.m = e;` — membro estático e valor associado (plano 21).
//
// Não há fase nova no pipeline nem nó novo na Core: `User.defaultAge` já é o
// `CoreField(CoreVariable "User", "defaultAge")` que o M2 produzia, e o que mudou
// foi o checker ganhar uma terceira leitura para ele. O valor vive num `Let`
// comum, sob um nome sintético que não é escrevível em fonte.
//
// Um membro não colide com um `def` comum de mesmo nome: são símbolos distintos,
// porque o segundo só é alcançável através de um tipo.
// expect: output
// 30
// User { name: "Gabriel", age: 30 }
// função solta
// ---
def User = type {
    name: Str;
    age: Int;
};

def User.defaultAge = 30;

def User.create = fn(nome: Str) User {
    return .User { name: nome, age: User.defaultAge };
};

// O mesmo nome, sem dono: continua valendo.
def create = "função solta";

print(User.defaultAge);
print(User.create("Gabriel"));
print(create);
