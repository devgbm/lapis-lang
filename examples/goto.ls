// `goto` e `label` — controle de fluxo explícito.
//
// Um `label` é um destino de salto local à função, como `return`; um `goto` é um
// desvio para ele, incondicional ou com `if`. Não são expressões: um salto não
// produz valor.
//
// O desugar decompõe o bloco em blocos básicos — cada `label` abre um join
// point, e o segmento anterior é fechado com um salto implícito. É por isso que
// nomes declarados *antes* do primeiro salto continuam visíveis no destino, e
// nomes declarados *entre* o salto e o rótulo não: o salto pode tê-los pulado.
//
// Saltar para trás é permitido, e com ele a terminação deixa de ser garantida
// por construção. O evaluator conta saltos e aborta com LAP0303 em vez de
// travar.
//
// Saída esperada:
//   -1
//   20
//   30
//   fim

def valores = [10, 20, 30];

// Saída antecipada: dois `goto` no topo evitam o aninhamento que a mesma
// verificação exigiria com `if`.
def buscar = fn(indice: Int) Int {
    goto invalido if indice < 0;
    goto invalido if indice > 2;

    match valores[indice] {
        Result.Ok(v) => return v,
        Result.Err(e) => return -1
    }

    label invalido;
    return -1;
};

print(buscar(9));
print(buscar(1));
print(buscar(2));

// `goto L if c` é primitivo, não açúcar para `if c { goto L }`: uma macro de
// controle construída sobre salto não pode depender do `if` da linguagem, senão
// a construção seria circular.
def pular = false;

goto fim if pular;
print("fim");
label fim;
