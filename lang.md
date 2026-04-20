```
def integer = 1;
def string = 'hello world';
def boolean = true;
def decimal 1.23;
def Person = type { age: integer; name: string; };
def func = func (integer a, integer b) { return 1 + 1; };
def Person.create = func(string name, integer age) Person { return Person { name: name, age: age } };
def Person.talk = func(self, string words) Void { Console.log(self.name + ' says: ' + words) }
def dict = .{ a: 1, b: 'hello' };
def array = .[ 1, 2, 3 ];
```


Types

```
primitives:

Int
Bool
Dec
Str
Type
Void

complex:

Func(Int, String):Person
Array(Int)
Dict(key: Type, ... )


literals:
1
'literal'
true
false


```

```
def a = type { a: string; };
def b = type { b: string; };
def ab = a & b; // type { a: string; b: string; };

def x = type { x: integer; a: string };
def y = type { y: integer; a: string };
def z = x & b; // type { x: integer; a: string; y: integer; }; 

def zWithoutA = z !& a; // type { x: integer; y: integer; };

def zHasA = z has a; // true;
```


```
def person = .{ name: 'José'; age: 23 };
def a = typeof dict; // type { name: string; age: integer; }

def integerAlias = typeof 1; //
```


```
def person = .{ name: 'José'; age: 23 };
def PersonType = typeof person;
def keysOfPersonType = PersonType.properties(); // [.{ propertyName: 'name', propertyType: string }, .{ propertyName: 'age', propertyType: integer }];
```



```
def addOne = func(Integer a) Integer {
    return a + 1;
}

def adderFactory = func(Integer a) (Integer):Integer {
    return func(Integer v) Integer {
        return a + v;
    }
}

def a = addOne(1); // 2;

def addTwo = adderFactory(2); // (Integer): Integer;
def b = addTwo(2); // 4;
```


```
def person = .{ name: 'John', age: 21 };
def toArray = func(t value) [t] {
    return []
}


// person.ls
module person;

def Identifier = func(prefix p) Self {
    return func() String {
        return p + gen();
    }
}

def PersonId = Identifier('per_');

def Person = type {
    id: typeof PersonId    @generatedAs Guid.new;
    name: string           @check maxlenght(32);
    age: integer           @check min(0);
    active: boolean        @generatedAs true;
};


Meta(Person).properties.id.generatedAs = Guid.new;

def Person.activate = func (self) {
    self.active = true;
}

def Person.deactivate = func(self) {
    self.active = false;
}

// expands into
def Person.Create = construct(Person);


macro construct($type:TypeSymbol) {
    func(#{type.properties().where($.generatedAs not unkown).select(#{$.name $type})}) $type {
        return $type {

        }
    }
}



func(string name, integer age, boolean active) Person | Error {

    //macro
    @assert name maxLenght 32 return Error('Nome deve conter no minimo 32 caracteres.');
    //expands into
    var validation = Validate(name).maxLenght(32, 'Nome deve conter no minimo 32 caracteres.');
    if(validation.isError) return validation.Error;


    Validate(age).min(0,);

    @assert age min 0 return Error('Idade deve ser maior que zero.');


    return Person {
        id: PersonId(),
        name: name,
        age: age,
        active: true
    };
};
```