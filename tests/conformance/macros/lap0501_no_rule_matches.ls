// expect: error LAP0501 at 6:1
macro inc
    match Identifier:n
    expand { print(n); };

@inc 10;
