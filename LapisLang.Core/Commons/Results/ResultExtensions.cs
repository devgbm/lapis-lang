
using System;

namespace LapisLang.Core;


public static class ResultExtensions
{
    public static void OnError(this Result result, Action<Error> action)
    {
        if(result.IsError) action(result.ErrorValue);
    }
}