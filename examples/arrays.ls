// Arrays e indexação (spec §18, §20, §21, §50).
// Indexação sempre devolve Result<T, IndexError>.
// Saída esperada:
//   Ok(10)
//   Ok(30)
//   Err(OutOfBounds)

def numbers = [10, 20, 30];

print(numbers[0]);
print(numbers[2]);
print(numbers[3]);
