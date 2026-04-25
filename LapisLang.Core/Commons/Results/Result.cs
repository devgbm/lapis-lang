namespace LapisLang.Core;

public class Result : Result<Empty>
{
    public Result() : base(new Empty()) { }
    public Result(Error error) : base(error) { }

    public static Result Ok() => new Result();
    public static Result<T> Ok<T>(T value) => new Result<T>(value);
    public static Error Error(string? message) => new Error(message);

    public static implicit operator Result(Error failure) => new Result(failure);
}
