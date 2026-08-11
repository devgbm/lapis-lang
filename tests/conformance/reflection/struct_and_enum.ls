// `reflect` sobre um `type` e sobre um `enum` (plano 19 §19.1).
//
// Um `TypeInfo` só serve os dois: `kind` diz qual é, `fields` fica vazio em enums
// e `variants` em structs.
// expect: output
// User
// [FieldInfo { name: "id", typeName: "Int" }, FieldInfo { name: "name", typeName: "Str" }]
// []
// Color
// [VariantInfo { name: "Red", arity: 0, payloadTypeNames: [] }, VariantInfo { name: "Green", arity: 0, payloadTypeNames: [] }]
// []
// ---
def User = type {
    id: Int;
    name: Str;
};

def Color = enum {
    Red,
    Green
};

print(reflect(User).name);
print(reflect(User).fields);
print(reflect(User).variants);

print(reflect(Color).name);
print(reflect(Color).variants);
print(reflect(Color).fields);
