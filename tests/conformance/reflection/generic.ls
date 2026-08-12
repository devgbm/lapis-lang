// Um `MetaType` genérico **não instanciado** é aceito de propósito:
// `reflect(Result)` descreve a declaração, com `typeParameterNames` preenchido.
// É o que uma constraint precisa — nomes e aridade, não os argumentos de uma
// instância (plano 19 §19.2).
//
// Escrito com argumentos, descreve a instância: o payload de `Ok` deixa de ser
// `T` e passa a ser `Int`. É a diferença entre descrever a declaração e descrever
// uma instância dela, e as duas são leituras legítimas.
//
// Repare também que indexar um span de metadados devolve `Option`, como qualquer
// outro span de tamanho desconhecido (plano 24): reflection não escapa das regras
// da linguagem. Os campos do `TypeInfo` são `[T;?]` porque a quantidade depende do
// tipo refletido, e não há `N` para escrever.
// expect: output
// ["T", "E"]
// ["T"]
// ["Int"]
// ---
def nenhum: [Str;?] = .[];

def payloadDaPrimeiraVariante = fn(info: TypeInfo) [Str;?] {
    match info.variants[0] {
        Option.Some(variante) => return variante.payloadTypeNames,
        Option.None => return nenhum
    }
};

print(reflect(Result).typeParameterNames);
print(payloadDaPrimeiraVariante(reflect(Result)));
print(payloadDaPrimeiraVariante(reflect(Option<Int>)));
