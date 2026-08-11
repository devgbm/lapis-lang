// Um literal sintático pertence à macro, não à linguagem: `in` não vira palavra
// reservada, e nenhuma outra construção passa a conhecê-lo (spec de macros §5).
// expect: output
// 7
// ---
macro bind
    match Identifier:nome in Expression:valor Block:corpo
    expand {
        def nome = valor;
        corpo;
    };

@bind x in 7 {
    print(x);
}

// E `in` continua sendo um identificador comum em qualquer outro lugar.
def in = 1;
