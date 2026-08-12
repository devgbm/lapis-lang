// Um rótulo de `loop` é local à função, como `return`: o `break` não atravessa
// a fronteira.
// expect: error LAP0525 at 5:32
loop :fora {
    def f = fn() Void { break :fora; };
    f();
};
