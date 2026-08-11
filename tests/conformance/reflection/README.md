# Casos de reflection

Metadados do programa como **valores comuns** (spec de macros §11, plano 19).

O ponto destes casos é que reflection não tem caminho especial nenhum: um
`TypeInfo` é um struct declarado no `prelude.ls`, e tudo o que vale para struct
vale para ele — imutabilidade, igualdade estrutural, indexação devolvendo
`Result`. É por isso que `reflect` custa tão pouco: não há sistema paralelo a
manter.
