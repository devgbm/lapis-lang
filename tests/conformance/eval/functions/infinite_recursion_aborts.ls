// Recursão sem caso base termina em diagnóstico, não em travamento nem em
// queda do processo (Q34).
//
// O `LAP0302` existia desde o M1 e era inalcançável — a suíte o registrava
// como isento na cobertura de diagnósticos. Este é o primeiro programa que o
// alcança. O evaluator roda em pilha própria justamente para que o limite seja
// o **da linguagem**, e não o do host: na pilha padrão o processo cairia por
// volta da chamada 1.300, antes do orçamento.
// expect: error LAP0302
// expect: abort
def semFim = fn(n: Int) Int {
    return semFim(n);
};

print(semFim(1));
