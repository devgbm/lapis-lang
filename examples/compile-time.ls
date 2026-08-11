// `constraint` — código que roda durante a compilação.
//
//     lapis run examples/compile-time.ls
//
// Entre o `match` e o `expand` de uma macro cabe um `constraint`: um bloco de
// LapisLang que valida a construção e acumula estado. Não é uma segunda
// linguagem — é a mesma, avaliada pelo mesmo evaluator (spec de macros §8.1). O
// que muda é só o ambiente:
//
//     runtime       `print`
//     compile time  `print`, `throw`, as primitivas de contexto
//
// A diferença entre falhar no `match` e falhar no `constraint` é a diferença
// entre "não é esta a forma" e "esta forma está errada" (§7.1): a primeira faz o
// mecanismo continuar procurando, a segunda encerra a compilação.
//
// Saída esperada:
//   POST /produtos
//   criando produto
//   POST /usuarios
//   criando usuário
//   GET /produtos
//   listando produtos

// O exemplo canônico: um roteador que recusa duas rotas iguais.
//
// A tabela de rotas vive no **contexto de compilação**, a única coisa mutável do
// sistema — e só enquanto a compilação dura. Não é um recurso da linguagem: é
// estado do compilador exposto por primitivas, do mesmo jeito que `print` expõe
// I/O. A regra "bindings são imutáveis" segue intacta.
macro post
    match Str:path Block:handler

    constraint {
        def key = "route.POST." + path;

        // `contextHas` responde Bool; `contextGet` responde `Result`, porque
        // chave ausente é falha **esperada** e falha esperada aparece no tipo.
        if contextHas(key) {
            // `throw` interrompe a compilação com esta mensagem, no span da
            // invocação. Tem tipo `Never`, como `return`, e é exclusivo de
            // compile time: fora de um `constraint` é LAP0507.
            throw "rota POST já registrada: " + path;
        }

        contextPut(key, path);
    }

    expand {
        print("POST " + path);
        handler;
    };

macro get
    match Str:path Block:handler

    constraint {
        def key = "route.GET." + path;

        if contextHas(key) {
            throw "rota GET já registrada: " + path;
        }

        contextPut(key, path);
    }

    expand {
        print("GET " + path);
        handler;
    };

@post "/produtos" {
    print("criando produto");
}

@post "/usuarios" {
    print("criando usuário");
}

// Métodos diferentes não colidem: a chave carrega o método, e é a chave que
// decide. Trocar este `get` por um `post` faz a compilação parar com LAP0503.
@get "/produtos" {
    print("listando produtos");
}
