// spec §14 — `type` definido pelo usuário, construído com ponto inicial (Q2).
// expect: output
// User { id: 1, name: "Gabriel" }
// Gabriel
// 7
// ---
def User = type {
    id: Int;
    name: Str;
};

def user = .User { id: 1, name: "Gabriel" };

print(user);
print(user.name);

def Box = type<T> { value: T; };

print(.Box<Int> { value: 7 }.value);
