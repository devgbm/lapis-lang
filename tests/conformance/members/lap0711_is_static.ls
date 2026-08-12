// E o contrário: `create` não tem `self`, então é do tipo e não da instância.
//
// O nome do primeiro parâmetro **é** a assinatura, e é frágil — trocar `self` por
// `this` muda o membro de instância para estático. É o preço de não ter sintaxe
// de método, e este diagnóstico é o que torna o erro legível.
// expect: error LAP0711 at 15:9
def User = type { name: Str; };

def User.create = fn(nome: Str) User {
    return .User { name: nome };
};

def u = User.create("g");

print(u.create("x"));
