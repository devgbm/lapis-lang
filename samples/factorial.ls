def factorial = func (Int n) Int: {
    if (n <= 1) {
        return 1;
    } else {
        return n * factorial(n - 1);
    }
};

Console.write(factorial(1).toString());
Console.write(factorial(5).toString());
Console.write(factorial(10).toString());
