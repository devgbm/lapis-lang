// A mensagem de `throw` vira o texto do LAP0503, então ela tem de ser Str.
// expect: error LAP0508 at 5:24
macro m
    match Str:s
    constraint { throw 1; }
    expand { print(s); };

@m "oi";
