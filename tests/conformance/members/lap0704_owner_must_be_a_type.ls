// O dono de um membro precisa ser um tipo **já declarado**. A ordem do topo é
// sequencial (Q8), então um membro antes do `type` cai aqui pela mesma razão que
// qualquer nome usado antes da declaração.
// expect: error LAP0704 at 7:5
def x = 1;

def x.m = 1;
