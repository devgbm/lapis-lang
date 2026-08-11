// Um `MetaType` genérico **não instanciado** é aceito de propósito:
// `reflect(Result)` descreve a declaração, com `typeParameterNames` preenchido.
// É o que uma constraint precisa — nomes e aridade, não os argumentos de uma
// instância (plano 19 §19.2).
//
// Escrito com argumentos, descreve a instância: o payload de `Ok` deixa de ser
// `T` e passa a ser `Int`. É a diferença entre descrever a declaração e descrever
// uma instância dela, e as duas são leituras legítimas.
//
// Repare também que indexar um array de metadados devolve `Result`, como qualquer
// outro array (spec §21): reflection não escapa das regras da linguagem.
// expect: output
// ["T", "E"]
// ["T"]
// ["Int"]
// ---
def nenhum: Str[] = [];

def payloadDaPrimeiraVariante = fn(info: TypeInfo) Str[] {
    match info.variants[0] {
        Result.Ok(variante) => return variante.payloadTypeNames,
        Result.Err(erro) => return nenhum
    }
};

print(reflect(Result).typeParameterNames);
print(payloadDaPrimeiraVariante(reflect(Result)));
print(payloadDaPrimeiraVariante(reflect(Result<Int, IndexError>)));
