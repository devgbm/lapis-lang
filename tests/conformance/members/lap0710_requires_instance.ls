// `User.saudar` é membro de **instância**: acessá-lo pelo tipo não faz sentido,
// porque não há `self` para dar.
//
// Sem essa separação, `User.saudar` ficaria ambíguo entre "o membro" e "a função
// não aplicada" — e a segunda leitura tornaria `u.saudar()` e `User.saudar(u)`
// dois caminhos para a mesma coisa (plano 22 §22.3).
// expect: error LAP0710 at 12:12
def User = type { name: Str; };

def User.saudar = fn(self) Str { return self.name; };

print(User.saudar());
