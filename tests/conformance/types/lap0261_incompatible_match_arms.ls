// expect: error LAP0261
// ---
def x = match 1 {
    1 => 1,
    _ => "texto"
};
