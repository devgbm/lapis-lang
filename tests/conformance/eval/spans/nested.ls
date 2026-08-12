// Span de spans: o tamanho de cada nível vive no tipo — `[[Int;2];2]` aqui,
// depois de os dois elementos terem o mesmo tipo.
// expect: output
// [[1, 2], [3, 4]]
// [3, 4]
// ---
def m = .[.[1, 2], .[3, 4]];

print(m);
print(m[1]);
