def fib = func (Int n) Int: {
    if (n <= 1) {
        return n;
    } else {
        return fib(n - 1) + fib(n - 2);
    }
};

Console.write('write a number');
var num = Int.parse(Console.read());
Console.write(fib(num).toString());
