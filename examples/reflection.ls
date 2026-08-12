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
// que produz `Option<T>` sem existir assinatura escrita para ela.
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

// Indexar um span de metadados devolve `Option`, como qualquer outro span de
// tamanho desconhecido: a reflection não abre exceção nas regras da linguagem.
// Os campos do `TypeInfo` são `[T;?]` porque a quantidade depende do tipo
// refletido, e não há `N` para escrever.
def primeiraVariante = fn(info: TypeInfo) Str {
    match info.variants[0] {
        Option.Some(variante) => return variante.name,
        Option.None => return "<sem variantes>"
    }
};

print(primeiraVariante(reflect(Color)));

// Um tipo genérico **não instanciado** descreve a declaração...
print(reflect(Result).typeParameterNames);

def payload = fn(info: TypeInfo) [Str;?] {
    match info.variants[0] {
        Option.Some(variante) => return variante.payloadTypeNames,
        Option.None => return reflect(Result).typeParameterNames
    }
};

// ...e escrito com argumentos, descreve a instância: o payload de `Ok` deixa de
// ser `T` e passa a ser `Int`.
print(payload(reflect(Option<Int>)));

// Igualdade estrutural, herdada de struct — nada de especial acontece aqui.
print(reflect(User) == reflect(User));
