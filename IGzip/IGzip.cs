using System.Runtime.InteropServices;

namespace IGzip;

public static class IGzip
{
    public enum DecompResult
    {
        // No errors encountered while decompressing
        DecompOk = 0,

        // End of input reached
        EndInput = 1,

        // End of output reached
        OutOverflow = 2,

        // End of gzip name buffer reached
        NameOverflow = 3,

        // End of gzip comment buffer reached
        CommentOverflow = 4,

        // End of extra buffer reached
        ExtraOverflow = 5,

        // Stream needs a dictionary to continue
        NeedDict = 6,

        // Invalid deflate block found
        InvalidBlock = -1,

        // Invalid deflate symbol found
        InvalidSymbol = -2,

        // Invalid lookback distance found
        InvalidLookback = -3,

        // Invalid gzip/zlib wrapper found
        InvalidWrapper = -4,

        // Gzip/zlib wrapper specifies unsupported compress method
        UnsupportedMethod = -5,

        // Incorrect checksum found
        IncorrectChecksum = -6,
    }

    public const int MaxSize = 16 * 1024 * 1024;

    public const int StreamSpaceSize = 86 * 1024; // ≥ sizeof(inflate_state): 87368 (Linux, Mac)

    /// <summary>
    ///     Decompresses the provided compressed input data and writes the decompressed output to the provided output buffer.
    /// </summary>
    /// <param name="input">The compressed data to be decompressed, provided as a read-only span of bytes.</param>
    /// <param name="output">The buffer where the decompressed data will be written.</param>
    /// <param name="offset">Optional offset into the output buffer at which to start writing the decompressed data.</param>
    /// <param name="streamSpace">Optional buffer used for internal state management. Reset by this method.</param>
    /// <returns>The total number of bytes written to the output buffer.</returns>
    /// <exception cref="Exception">
    ///     Thrown when internal validation fails (e.g., if the offset of certain fields does not match
    ///     expectations).
    /// </exception>
    /// <exception cref="OutputBufferNotBigEnoughException">
    ///     Thrown when the output buffer is not large enough to contain the
    ///     decompressed data.
    /// </exception>
    public static int Inflate(ReadOnlySpan<byte> input, byte[] output, int offset = 0, byte[]? streamSpace = null)
    {
        if (Marshal.SizeOf<IGZipBase.InflateStateStart>() != 87368)
            throw new Exception(
                $"Size of InflateStateStart is {Marshal.SizeOf<IGZipBase.InflateStateStart>()}, not 87368");
        if (Marshal.OffsetOf<IGZipBase.InflateStateStart>("crc_flag") != 21172)
            throw new Exception(
                $"Offset of crc_flag is {Marshal.OffsetOf<IGZipBase.InflateStateStart>("crc_flag")}, not 21172");

        streamSpace ??= new byte[StreamSpaceSize];
        int total;
        unsafe
        {
            fixed (byte* pStreamSpace = streamSpace)
            fixed (byte* pInput = input)
            fixed (byte* pOutput = output)
            {
                var state = (IGZipBase.InflateStateStart*)pStreamSpace;
                IGZipBase.InflateInit(state);

                state->avail_in = input.Length;
                state->next_in = pInput;
                state->avail_out = output.Length - offset;
                state->next_out = pOutput + offset;
                state->crc_flag = IGZipBase.GzipFlags.Gzip;
                var result = (DecompResult)IGZipBase.Inflate(state);
                if (result != DecompResult.DecompOk /*&& result != IGZipBase.DecompResult.EndInput*/
                   ) throw new DecompressionException(result);
                if (state->avail_in != 0) throw new OutputBufferNotBigEnoughException();
                total = state->total_out;
            }
        }
        return total;
    }
}

public class DecompressionException(IGzip.DecompResult result) : Exception
{
    public IGzip.DecompResult Result { get; } = result;
    
    public override string ToString()
    {
        return $"Decompression failed with result: {Result}";
    }
}
