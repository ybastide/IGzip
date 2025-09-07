namespace IGzip;

public static class IGzip
{
    public const int MaxSize = 16 * 1024 * 1024;

    public static int Inflate(ReadOnlySpan<byte> input, byte[] output, int offset = 0)
    {
        int total = 0;
        unsafe
        {
            byte[] streamSpace = new byte[IGZipBase.InflateStateStructSize];
            fixed (byte* pStreamSpace = streamSpace)
            fixed (byte* pInput = input)
            fixed (byte* pOutput = output)
            {
                IGZipBase.InflateStateStart* state = (IGZipBase.InflateStateStart*)pStreamSpace;

                state->avail_in = input.Length;
                state->next_in = pInput;
                state->avail_out = output.Length - offset;
                state->next_out = pOutput + offset;
                var result = (IGZipBase.DecompResult)IGZipBase.Inflate(state);
                if (result != IGZipBase.DecompResult.DecompOk && result != IGZipBase.DecompResult.EndInput)
                    throw new Exception($"Decompression failed with error code {result}");
                total = state->total_out;
            }
        }
        return total;
    }
}
