Console.WriteLine("Hello, World!");
byte[] compressed;
try
{
    var path = args.Length > 0 ? args[0] : "fixtures/Gorgosaurus.gz";
    compressed = File.ReadAllBytes(path);
}
catch (DirectoryNotFoundException)
{
    compressed = File.ReadAllBytes("../../../../TestIGzip/fixtures/Gorgosaurus.gz");
}
var output = new byte[IGzip.IGzip.MaxSize];
var size = IGzip.IGzip.Inflate(compressed, output);
Console.WriteLine($"Input size: {compressed.Length}; output size: {size}");
