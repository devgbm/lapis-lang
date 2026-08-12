// Um membro é definitivo. Um membro mutável exigiria decidir onde vive o slot —
// pergunta que a Q25 fechou para bindings, e que não vale reabrir por uma forma
// que ninguém pediu.
// expect: error LAP0705 at 7:1
def User = type { name: Str; };

var User.m = 1;
