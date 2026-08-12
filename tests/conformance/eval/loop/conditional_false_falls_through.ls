// Com condição falsa a execução simplesmente segue.
// expect: output
// no meio
// depois
// ---
def pular = false;

loop {
    if pular { break; }
    print("no meio");
    break;
}

print("depois");
