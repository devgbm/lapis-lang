// expect: error LAP0255
// ---
def User = type { id: Int; };
def u = .User { id: 1, id: 2 };
