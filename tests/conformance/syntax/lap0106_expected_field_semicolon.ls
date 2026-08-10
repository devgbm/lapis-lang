// LAP0106 — campo de `type` sem o `;` que o separa do próximo (spec §14).
// expect: error LAP0106 at 5:5
def Point = type {
    x: Int
    y: Int;
};
