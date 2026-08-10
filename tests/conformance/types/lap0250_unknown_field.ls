// expect: error LAP0250
// ---
def User = type { id: Int; };
def u = .User { id: 1 };
def x = u.inexistente;
