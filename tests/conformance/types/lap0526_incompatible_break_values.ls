// expect: error LAP0526
def x = loop {
    if true { break 1; }
    break "a";
};
