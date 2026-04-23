# Lapis Lang

## Executando arquivo
Lapis pode ser executado direto da linha de comando sem necessidade de compilar. é executado com o comando `lapis ./hello-world.ls`. Isso roda o arquivo hello-world.ls usando o interpretador do lapis.

```
import Console;

Console.write('Hello World!');
```

## Criando Projetos
Um projeto lapis pode ser criado usando o comando `lapis --new <template-name> --name <project-name>` este comando cria um projeto lapis usando o template template-name novo com o nome project-name no subdiretório ./\<project-name> com a seguinte estrutura de arquivos:

```
/project-name
    /src
        entry.ls
    lapis.proj.yml
```

O arquivo `lapis.proj.yml` é o arquivo de configurações do projeto, inclui dependencias versões, tipo de projeto, etc. Segue um exemplo abaixo:

```yml
project:
  - name: HelloWorld            # Project name 
  - type: executable            # Project type, could be exe, lib atm
  - version: 1.0.0              # Project version, optional
  - build:                      # Build configurations, only source atm
    - source: ./src/**.ls       # Source files
  - dependencies:               # Dependencies
    - lapis.std: 1.0.0          # dependecy and version
    - etc: 0.1.0
```



## Language


## Tipos Primitivos

Existem os seguintes tipos primitivos:

**Str**: Representa uma string literal terminada em nulo.

 `'Isso é uma string literal'`

**Int**: Numerico intero com ou sem sinal.

`1   -1  10 20`

**Bool**: True ou False

`true false`

**Dec**: Numero com ponto flutuante

`1.0  3.141592 -3.141592`

**Unkown** Tipo desconhecido, usado para representar erros de compilação

**Void** Tipo vazio, usado em retorno de funções


### Outros Tipos


**Func**: Tipos de Função:

`Func<Int, Str: Bool>` tipo de função que recebe Int e Str como parametro e retorna Bool


**Type**: Meta tipo primitivo, pode representar tipos primitivos, complexos e de função.

`Int Bool Dec Type CustomType Func<Int:Bool>`

## Operadores Logicos

A linguagem usa operadores logicos com palavras reservadas, os seguintes operadores existem:

```
or  operador logico or
and operador logico and
not operador logico not
>= Maior igual
> Maior
< Menor
<= Menor igual
== Igualdade
!= Desigualdade
```

## Operadores Matemáticos
```
+  Soma
- Subtração
* Multiplicação
/ Divisao
% Modulo
```


## Tipos complexos
Em lapis tipos são tratados como valores e é possivel declarar tipos complexos com a palavra reservada `type` Ex:

`type { a: Str }`

define um tipo complexo com a propriedade a como Str.


## Tipos de Função
Em lapis funções são tratadas como valores e é possivel declarar funções com  a palavra reservada `func` Ex:

```
// Função que recebe dois ints e retorna Bool
func (Int a, Int b) Bool :{
    // ... implementação
}

// Função sem parametro e sem retorno
func () Void :{
    // ... implementação
}
```

## Tipos Enumerados
Tipos enumerados podem ter um ou mais tipo associados a ele podendo ter dados ou não. no exemplo abaixo é definido um tipo Opcional onde um valor pode ou naõ existir.

```
def OptionalInt = enum { Some(Int value), None }

def maybeOne = OptionalInt.Some(1);
```

## Definições
Como em lapis tudo são simbolos use a palavra reservada `def` para dar nome a um simbolo. No exemplo abaixo, define-se uma função que verifica se um numero é maior que 10 e retorna true ou false:


```
// Define-se uma função
def maiorQue10 = func (Int a) Bool :{
    return a > 10;
};

//Define-se Pi
def pi = 3.141592;

//Define-se o tipo pessoa
def Pessoa = type {
    id: Int;
    name: Str;
};

```
Nas definições o sinal de igual é opcional, portanto as definições abaixo são equivalentes

```
def Pessoa = type {
    id: Int;
    name: Str;
};
// equivalente a
def Pessoa type {
    id: Int;
    name: Str;
};

```

Definições são constantes e imutaveis, portante não é possivel redefinir um valor apos ser inicialmente definido, para isso use-se `var` (Ver var).


**Type**: Representa um tipo

`Int Bool MyComplexType`


### Operadores

No lapis existem alguns operadores

Tudo na linguagem são simbolos (Symbols) inclusive literais, os simbolos podem ser resolvidos em valores, funções, tipos, namespaces.

para definir um simbolo usa-se a palavra reservada `def`seguida do nome do simbolo um sinal de igual (opcional) e o valor do simbolo. Ex:

```
// Isso define a sendo 1;
def a = 1;

// Isso define b sendo a, aqui o sinal de = é omitido.
def b a;
```


## Imports
Imports são usados para fazer referencia de outros arquivos. por exemplo

```
import Console; // importa o simbolo console. no momento da execução/compilação é buscado esse simbolo das dependencias


import MyFoo from 'lib/my-foo.ls'; // importa a função MyFoo do arquivo lib/my-foo (caminho relativo a raiz do projeto ou arquivo sendo executado) para isso a função my-foo deve estar marcada como export;
```

## Modulos

Em lapis pode-se criar e definir modulos com a palavra reservada `module`. Modulos são como namespaces usados para agrupar symbolos e funcionalidades.
No exemplo abaixo cria-se o modulo e define-se os simbolos desse modulo


```
// Cria-se o modulo
def MyLib = module;

// Define a função foo dentro do modulo
def MyLib.foo = func()Void:{
    // implementação
}
```

Modulos tambem podem ser exportados para serem referenciados por outros arquivos fonte
```
// define-se o modulo ('='' omitido)
def MyLib module;


// definições do modulo

export MyLib;
```

modulos declarados pelo programa sendo escrito podem ser reimportados por outros arquivos para extender suas definições. Ex o modulo é definido em um arquivo

```
//lib/etc/module.ls
def Etc module;

//lib/etc/a.ls
import Etc from 'lib/etc/module';
// aqui é definido a sendo igual a 1
def Etc.a  = 1;

//lib/etc/b.ls
import Etc from 'lib/etc/module';
// aqui é definido b sendo igual a 2
def Etc.b  = 2;

//src/main.ls
import Etc from 'lib/etc/module';

// Pode-se usar a e b
def c = Etc.a + Etc.b;
```

### Metodos
Metodos são funções associadas as tipos, podendo ser estáticos ou de instancia
No exemplo abaixo é definido o metodo Add ao tipo Int.

```
//define o metodo estático add no tipo Int
def Int.add = func(Int a, Int b) Int: {
    return a + b;
};

// Função invocada
def tres = Int.add(1, 2);
```

metodos de instancia utilizam a palavra reservada self

```
//define o metodo de instancia add no tipo Int
def Int.add = func(self, Int b) Int: {
    return self + b;
};

// Função invocada
def tres = 1.add(2);
```



### Tipo Self
é possivel que um tipo se auto-referencie usando o tipo Self. No exemplo abaixo o tipo Self é usado para criar uma linked list de Int

```
def LinkedListInt = type {
    current: Int;
    next: Opt<Self>
};
```

 quando usada em uma função representa a propria função sendo declarada 







