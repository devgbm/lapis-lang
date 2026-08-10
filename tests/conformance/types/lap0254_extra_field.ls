// expect: error LAP0254
// ---
def User = type { id: Int; };
def u = .User { id: 1, sobrando: 2 };
