// `T.m` é a terceira leitura de acesso a membro, depois de campo de instância e
// variante de enum (plano 21 §21.5). Quando o tipo não tem o membro, a mensagem
// diz isso — e não "campo desconhecido", que falaria de outra coisa.
// expect: error LAP0701 at 7:12
def User = type { name: Str; };

print(User.naoExiste);
