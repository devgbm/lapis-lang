// O exemplo canônico da spec de macros §8.3: `@post` registra a rota no contexto
// de compilação, e a constraint recusa a segunda com o mesmo caminho.
//
// Aqui as rotas são distintas, então tudo compila e roda.
// expect: output
// /produtos
// 1
// /usuarios
// 2
// ---
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
@post "/usuarios" { print(2); }
