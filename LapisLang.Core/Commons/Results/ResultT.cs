using System.Diagnostics.CodeAnalysis;

namespace LapisLang.Core;

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
