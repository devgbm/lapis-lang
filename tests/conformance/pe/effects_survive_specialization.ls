// `print` nunca executa em tempo de PE, e nenhum efeito é duplicado, eliminado
// ou reordenado — mesmo quando o valor é descartado.
// expect: output
// 1
// 2
// 3
// ---
def descartado = print(1);
print(2);
print(3);
