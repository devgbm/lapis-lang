// Com condição falsa a execução simplesmente segue — por isso `goto ... if` tem
// tipo Void, e não Never.
// expect: output
// no meio
// depois
// ---
def pular = false;
goto fim if pular;
print("no meio");
label fim;
print("depois");
