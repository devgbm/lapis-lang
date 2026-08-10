// `goto L if e` com condição verdadeira salta.
// expect: output
// depois
// ---
def pular = true;
goto fim if pular;
print("pulado");
label fim;
print("depois");
