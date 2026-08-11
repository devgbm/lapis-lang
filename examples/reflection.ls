// Reflection — metadados do programa como valores comuns.
//
//     lapis run examples/reflection.ls
//     lapis pe  examples/reflection.ls    # veja `reflect(...).name` sumir
//
// O ponto é o que **não** existe aqui: não há sistema de metadados paralelo. Um
// `TypeInfo` é um `type` declarado no `prelude.ls`, e tudo o que vale para struct
// vale para ele — igualdade estrutural, imutabilidade, indexação devolvendo
// `Result`. É por isso que reflection custa tão pouco (spec §58.2).
//
// `reflect` é um **intrínseco**, não uma função: o argumento tem de ser um tipo, e
// "um tipo" não é expressável na gramática de tipos. Mesmo estatuto da indexação,
// que produz `Result<T, IndexError>` sem existir assinatura escrita para ela.
//
// Saída esperada:
//   User
//   TypeKind.Struct
//   [FieldInfo { name: "id", typeName: "Int" }, FieldInfo { name: "name", typeName: "Str" }]
//   Color
//   Red
//   ["T", "E"]
//   ["Int"]
//   true

def User = type {
    id: Int;
    name: Str;
};

def Color = enum {
    Red,
    Green,
    Blue
};

print(reflect(User).name);
print(reflect(User).kind);
print(reflect(User).fields);

print(reflect(Color).name);

// Indexar um array de metadados devolve `Result`, como qualquer outro array: a
// reflection não abre exceção nas regras da linguagem.
def primeiraVariante = fn(info: TypeInfo) Str {
    match info.variants[0] {
        Result.Ok(variante) => return variante.name,
        Result.Err(erro) => return "<sem variantes>"
    }
};

print(primeiraVariante(reflect(Color)));

// Um tipo genérico **não instanciado** descreve a declaração...
print(reflect(Result).typeParameterNames);

def payload = fn(info: TypeInfo) Str[] {
    match info.variants[0] {
        Result.Ok(variante) => return variante.payloadTypeNames,
        Result.Err(erro) => return reflect(Result).typeParameterNames
    }
};

// ...e escrito com argumentos, descreve a instância: o payload de `Ok` deixa de
// ser `T` e passa a ser `Int`.
print(payload(reflect(Result<Int, IndexError>)));

// Igualdade estrutural, herdada de struct — nada de especial acontece aqui.
print(reflect(User) == reflect(User));
