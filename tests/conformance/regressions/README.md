# Regressões

Um arquivo por bug encontrado, nomeado `descricao.ls` ou
`issue-NNN-descricao.ls` quando houver issue.

**Regra do projeto: todo bug corrigido entra aqui antes da correção.** Um caso
que reproduz o bug e falha é o que prova que a correção corrigiu algo — e o que
impede o bug de voltar em silêncio.

O formato é o mesmo dos demais casos (plano 11 §11.1): diretivas `// expect:` no
cabeçalho. Vale acrescentar, em comentário, **como** o bug apareceu: quem lê daqui
a um ano precisa saber por que o caso existe.
