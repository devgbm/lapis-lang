// Um salto incondicional pula o que vem até o rótulo.
// expect: output
// 2
// ---
goto fim;
print(1);
label fim;
print(2);
