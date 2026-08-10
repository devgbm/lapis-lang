// Q9 especifica que `Int.MinValue / -1` dá a volta — mas o menor Int **não é
// escrevível**: o lexer lê `9223372036854775808` como literal positivo e o
// rejeita antes de o `-` unário chegar. O comportamento existe (há teste em C#
// sobre o evaluator), só não é alcançável a partir do fonte.
//
// skip: Int.MinValue não é escrevível como literal — ver apêndice C
// expect: output
// -9223372036854775808
// ---
print(-9223372036854775808 / -1);
