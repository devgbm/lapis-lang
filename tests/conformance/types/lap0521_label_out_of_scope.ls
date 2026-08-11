// Um rótulo é local à função, como `return`: o salto não atravessa a fronteira.
// expect: error LAP0521 at 5:10
label fora;
def f = fn() Void {
    goto fora;
};
f();
