// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
byte[] compressed;
try
{
    compressed = File.ReadAllBytes("fixtures/Gorgosaurus.gz");
} catch (System.IO.DirectoryNotFoundException)
{
    compressed = File.ReadAllBytes("../../../../TestIGzip/fixtures/Gorgosaurus.gz");
}
var output = new byte[IGzip.IGzip.MaxSize];
var size = IGzip.IGzip.Inflate(compressed, output);
Console.WriteLine($"Input size: {compressed.Length}; output size: {size}");
