// Um join pode terminar em outro salto: o evaluator continua no próximo join.
// expect: output
// terceiro
// ---
goto a;
label a;
goto c;
label b;
print("segundo");
label c;
print("terceiro");
