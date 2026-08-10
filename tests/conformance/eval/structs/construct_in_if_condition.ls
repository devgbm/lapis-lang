// Q2: o ponto inicial faz a construção ser reconhecível com um token, então ela
// vale até na condição de um `if`, sem parênteses.
// expect: output
// ativo
// ---
def Flag = type { on: Bool; };

if .Flag { on: true }.on {
    print("ativo");
}
