// expect: error LAP0260
// ---
def x = match 1 {
    "texto" => 1,
    _ => 2
};
