// expect: error LAP0504 at 3:11
macro m
    match Foo:x
    expand { print(x); };
