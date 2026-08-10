// Um array vazio não diz o que carrega; a anotação diz (spec §18). É o único
// lugar em que um tipo flui de cima para baixo neste checker.
// expect: output
// []
// Result.Err(IndexError.OutOfBounds)
// ---
def vazio: Int[] = [];

print(vazio);
print(vazio[0]);
