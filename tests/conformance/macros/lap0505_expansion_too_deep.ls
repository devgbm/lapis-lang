// Sem recursão (Q8) uma macro não se invoca, mas ciclos indiretos são
// construíveis — e terminam em diagnóstico, não em travamento.
// expect: error LAP0505 at 7:9
macro ping match Expression:e expand { @pong e };
macro pong match Expression:e expand { @ping e };

def r = @ping 1;
