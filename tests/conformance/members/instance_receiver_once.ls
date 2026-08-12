// O receptor é avaliado **exatamente uma vez** (plano 22 §22.4).
//
// É o erro clássico de desugaring de método: reescrever `proximo().saudar()` para
// `User#saudar(proximo())` duplicaria a expressão, e `proximo()` rodaria duas
// vezes. Aqui ela é avaliada no lugar em que está escrita, e o valor é passado.
//
// O receptor é uma expressão qualquer, não só um nome — é por isso que a
// resolução guarda o **nó**.
// expect: output
// avaliou
// g
// ---
def User = type { name: Str; };

def User.saudar = fn(self) Str {
    return self.name;
};

def proximo = fn() User {
    print("avaliou");
    return .User { name: "g" };
};

print(proximo().saudar());
