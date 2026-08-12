// O padrão com dono genérico e curinga (plano 25 §25.5) — `Result<Int, ?>.Ok(v)`
// aceita o `Result<Int, Str>` real sem repetir o erro.
// expect: output
// 10
// ---
def r: Result<Int, Str> = Result<Int, Str>.Ok(10);

if r is Result<Int, ?>.Ok(v) {
    print(v);
} else {
    print(0);
}
