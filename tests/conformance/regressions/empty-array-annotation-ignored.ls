// Encontrado ao escrever a propriedade `Indexing_AlwaysProducesResult` do M5.
//
// `LAP0241` mandava anotar o tipo do array vazio, mas a anotação não ajudava: o
// `CheckLet` checava o valor antes de resolvê-la, então `def a: Int[] = [];`
// falhava com a mensagem que pedia exatamente o que acabara de não funcionar.
// expect: output
// []
// ---
def a: Int[] = [];
print(a);
