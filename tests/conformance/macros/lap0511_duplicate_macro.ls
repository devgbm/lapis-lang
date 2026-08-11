// expect: error LAP0511 at 3:7
macro m match Expression:e expand { print(e); };
macro m match Identifier:i expand { print(i); };
