using Xunit.Abstractions;
namespace TestIGzip;

public class TestInflate(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void Test1()
    {
        var compressed = File.ReadAllBytes("fixtures/Gorgosaurus.gz");
        var output = new byte[IGzip.IGzip.MaxSize];
        var size = IGzip.IGzip.Inflate(compressed, output);
        testOutputHelper.WriteLine($"Input size: {compressed.Length}; output size: {size}");
        Console.WriteLine($"Input size: {compressed.Length}; output size: {size}");
    }
}