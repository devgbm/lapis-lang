// Não há overload: dois membros com o mesmo nome no mesmo tipo é erro.
//
// O diagnóstico é mais específico que `LAP0202` de propósito — "'create' já foi
// definido neste escopo" seria confuso, porque `create` sozinho não está definido
// em escopo nenhum.
// expect: error LAP0702 at 10:10
def User = type { name: Str; };

def User.create = fn() Str { return "a"; };
def User.create = fn() Str { return "b"; };
