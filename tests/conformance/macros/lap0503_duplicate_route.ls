// A mesma rota duas vezes: a constraint da segunda invocação encontra a chave já
// registrada e rejeita. O span é o da invocação — quem escreveu `@post` precisa
// ver a sua linha, não a da macro (plano 18 §18.6).
// expect: error LAP0503 at 24:1
macro post
    match Str:path Block:handler

    constraint {
        def key = "route.POST." + path;

        if contextHas(key) {
            throw "rota POST já registrada: " + path;
        }

        contextPut(key, path);
    }

    expand {
        print(path);
        handler;
    };

@post "/produtos" { print(1); }
@post "/produtos" { print(2); }
