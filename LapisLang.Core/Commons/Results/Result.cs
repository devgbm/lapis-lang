using System.Diagnostics.CodeAnalysis;

namespace LapisLang.Core;

public class Result : Result<Empty>
{
    public Result(): base(new Empty()) {}
    public Result(Error error): base(error){}

    public static Result Ok() => new Result();
    public static Result<T> Ok<T>(T value) => new Result<T>(value);
    public static Error Error(string? message) => new Error(message);

    public static implicit operator Result(Error failure) => new Result(failure);
}

public class Empty;
public class Result<T>
{
    public Result(T value)
    {
        Value = value;
    }

    public Result(Error error)
    {
        ErrorValue = error;
    }
    public T? Value { get; }
    public Error? ErrorValue { get; }
    [MemberNotNullWhen(true, nameof(ErrorValue))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool IsError { get => ErrorValue is not null; }
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(ErrorValue))]
    public bool IsSuccess { get => Value is not null; }
    public static implicit operator Result<T>(Error failure) => new Result<T>(failure);
    public static implicit operator Result<T>(T value) => new Result<T>(value);
}
