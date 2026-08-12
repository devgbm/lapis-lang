// Com condição verdadeira, o `break` sai do laço antes do que vem depois dele.
// expect: output
// depois
// ---
def pular = true;

loop {
    if pular { break; }
    print("pulado");
    break;
}

print("depois");
