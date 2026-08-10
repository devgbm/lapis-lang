// Bool tem exatamente dois valores, então dois literais o esgotam — sem `_`.
// expect: output
// sim
// ---
print(match true { true => "sim", false => "não" });
