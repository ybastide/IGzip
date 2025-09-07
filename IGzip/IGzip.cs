using System.Runtime.InteropServices;

namespace IGzip;

public static class IGzip
{
    public const int MaxSize = 16 * 1024 * 1024;

    /// <summary>
    /// Decompresses the provided compressed input data and writes the decompressed output to the provided output buffer.
    /// </summary>
    /// <param name="input">The compressed data to be decompressed, provided as a read-only span of bytes.</param>
    /// <param name="output">The buffer where the decompressed data will be written.</param>
    /// <param name="offset">Optional offset into the output buffer at which to start writing the decompressed data.</param>
    /// <param name="streamSpace">Optional buffer used for internal state management. Reset by this method.</param>
    /// <returns>The total number of bytes written to the output buffer.</returns>
    /// <exception cref="Exception">Thrown when internal validation fails (e.g., if the offset of certain fields does not match expectations).</exception>
    /// <exception cref="OutputBufferNotBigEnoughException">Thrown when the output buffer is not large enough to contain the decompressed data.</exception>
    public static int Inflate(ReadOnlySpan<byte> input, byte[] output, int offset = 0, byte[]? streamSpace = null)
    {
        if (Marshal.SizeOf<IGZipBase.InflateStateStart>() != 87368)
        {
            throw new Exception(
                $"Size of InflateStateStart is {Marshal.SizeOf<IGZipBase.InflateStateStart>()}, not 87368");
        }
        if (Marshal.OffsetOf<IGZipBase.InflateStateStart>("crc_flag") != 21172)
        {
            throw new Exception(
                $"Offset of crc_flag is {Marshal.OffsetOf<IGZipBase.InflateStateStart>("crc_flag")}, not 21172");
        }

        streamSpace ??= new byte[IGZipBase.InflateStateStructSize];
        int total;
        unsafe
        {
            fixed (byte* pStreamSpace = streamSpace)
            fixed (byte* pInput = input)
            fixed (byte* pOutput = output)
            {
                IGZipBase.InflateStateStart* state = (IGZipBase.InflateStateStart*)pStreamSpace;
                IGZipBase.InflateInit(state);

                state->avail_in = input.Length;
                state->next_in = pInput;
                state->avail_out = output.Length - offset;
                state->next_out = pOutput + offset;
                state->crc_flag = IGZipBase.GzipFlags.Gzip;
                Console.WriteLine($"next_out: {Marshal.OffsetOf(typeof(IGZipBase.InflateStateStart), "next_out")}");
                Console.WriteLine($"avail_out: {Marshal.OffsetOf(typeof(IGZipBase.InflateStateStart), "avail_out")}");
                Console.WriteLine($"total_out: {Marshal.OffsetOf(typeof(IGZipBase.InflateStateStart), "total_out")}");
                Console.WriteLine($"next_in: {Marshal.OffsetOf(typeof(IGZipBase.InflateStateStart), "next_in")}");
                Console.WriteLine($"read_in: {Marshal.OffsetOf(typeof(IGZipBase.InflateStateStart), "read_in")}");
                Console.WriteLine($"avail_in: {Marshal.OffsetOf(typeof(IGZipBase.InflateStateStart), "avail_in")}");
                Console.WriteLine($"crc_flag: {Marshal.OffsetOf(typeof(IGZipBase.InflateStateStart), "crc_flag")}");
                var result = (IGZipBase.DecompResult)IGZipBase.Inflate(state);
                if (result != IGZipBase.DecompResult.DecompOk /*&& result != IGZipBase.DecompResult.EndInput*/)
                {
                    throw new Exception($"Decompression failed with error code {result}");
                }
                if (state->avail_in != 0)
                {
                    throw new OutputBufferNotBigEnoughException();
                }
                total = state->total_out;
            }
        }
        return total;
    }
}
