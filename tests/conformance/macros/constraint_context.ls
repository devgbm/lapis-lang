// As primitivas de contexto (spec de macros §8.3).
//
// O caso é auto-verificável: a constraint confere o que ela mesma guardou e
// rejeita se algo não bater. Compilar limpo **é** a afirmação — `contextGet`
// devolve o que `contextPut` guardou, e devolve `Err` para chave ausente, que é
// falha esperada e por isso aparece no tipo (spec §30).
//
// A constraint não pode imprimir para provar isso: o que ela imprime é saída do
// compilador, não do programa.
// expect: output
// pronto
// ---
macro registra
    match Str:path

    constraint {
        if match contextGet(path) {
            Result.Ok(v) => true,
            Result.Err(e) => false
        } {
            throw "contextGet devolveu Ok para uma chave nunca escrita";
        }

        contextPut(path, "guardado");

        if !contextHas(path) {
            throw "contextHas não viu o que contextPut acabou de guardar";
        }

        if match contextGet(path) {
            Result.Ok(v) => v,
            Result.Err(e) => "faltou"
        } != "guardado" {
            throw "contextGet não devolveu o que contextPut guardou";
        }
    }

    expand { print("pronto"); };

@registra "/a";
