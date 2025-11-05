using System.Numerics;

namespace PragmaStack.Core;

public static class SampleClass
{
    public static INumber<T> AddNumbers<T>(T a, T b) 
        where T : INumber<T>
    {
        return a + b;
    }
}
