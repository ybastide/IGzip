using System.Runtime.InteropServices;

namespace IGzip;

public static class IGzip
{
    public const int MaxSize = 16 * 1024 * 1024;

    public static int Inflate(ReadOnlySpan<byte> input, byte[] output, int offset = 0)
    {
        if (Marshal.OffsetOf(typeof(IGZipBase.InflateStateStart), "crc_flag") != 21172)
        {
            throw new Exception("Offset of crc_flag is not 21172");
        }

        int total;
        unsafe
        {
            byte[] streamSpace = new byte[IGZipBase.InflateStateStructSize];
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
                    throw new Exception($"Decompression failed with error code {result}");
                total = state->total_out;
            }
        }
        return total;
    }
}
