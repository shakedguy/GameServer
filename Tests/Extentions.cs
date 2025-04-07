using System.Reflection;

namespace Tests;

public static class ReflectionExtensions
{
    public static async Task<T> InvokeAsync<T>(this MethodInfo methodInfo,
        object obj, params object[] parameters)
    {
        var task = (Task<T>)methodInfo.Invoke(obj, parameters);
        return await task;
    }
}