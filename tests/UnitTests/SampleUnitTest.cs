namespace UnitTests;

public class SampleUnitTest
{
    [Theory]
    [InlineData(2, 3, 5)]
    [InlineData(-1, 1, 0)]
    public void SampleClassShouldAddNumbersCorrectly(int a, int b, int expected)
    {
        // Arrange & Act
        var result = PragmaStack.Core.SampleClass.AddNumbers<int>(a, b);

        // Assert
        result.ShouldBeEquivalentTo(expected);
    }
}
