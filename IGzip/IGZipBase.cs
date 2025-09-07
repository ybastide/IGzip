using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;

namespace IGzip;

[SuppressUnmanagedCodeSecurity]
internal static class IGZipBase
{
    private const string NativeLib = "isal";

    public const int InflateStateStructSize = 86 * 1024; // ≥ sizeof(inflate_state): 87368 (Linux, Mac)

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
        IncorrectChecksum = -6
    }

    /******************************************************************************/
    /* Deflate Compression Standard Defines */
    /******************************************************************************/
    public const int IGZIP_K = 1024;
    public const int DEF_MAX_HDR_SIZE = 328;
    public const int DEF_MAX_CODE_LEN = 15;
    public const int DEF_HIST_SIZE = 32 * IGZIP_K;
    public const int DEF_MAX_HIST_BITS = 15;
    public const int DEF_MAX_MATCH = 258;
    public const int DEF_MIN_MATCH = 3;
    public const int DEF_LIT_SYMBOLS = 257;
    public const int DEF_LEN_SYMBOLS = 29;
    public const int DEF_DIST_SYMBOLS = 30;
    public const int DEF_LIT_LEN_SYMBOLS = DEF_LIT_SYMBOLS + DEF_LEN_SYMBOLS;

    // Max repeat length, rounded up to 32 byte boundary
    public const int LOOK_AHEAD = (DEF_MAX_MATCH + 31) & ~31;

    // Deflate Implementation Specific Constants
    // Note IGZIP_HIST_SIZE must be a power of two
    public const int IGZIP_HIST_SIZE = DEF_HIST_SIZE;
    public const bool LIMIT_HASH_UPDATE = true;
    public const int IGZIP_HASH8K_HASH_SIZE = 8 * IGZIP_K;
    public const int IGZIP_HASH_HIST_SIZE = IGZIP_HIST_SIZE;
    public const int IGZIP_HASH_MAP_HASH_SIZE = IGZIP_HIST_SIZE;
    public const int IGZIP_LVL0_HASH_SIZE = 8 * IGZIP_K;
    public const int IGZIP_LVL1_HASH_SIZE = IGZIP_HASH8K_HASH_SIZE;
    public const int IGZIP_LVL2_HASH_SIZE = IGZIP_HASH_HIST_SIZE;
    public const int IGZIP_LVL3_HASH_SIZE = IGZIP_HASH_MAP_HASH_SIZE;

#if LONGER_HUFFTABLE
    public const int IGZIP_DIST_TABLE_SIZE = 8 * 1024;
    public const int IGZIP_DECODE_OFFSET = 26;
#else
    public const int IGZIP_DIST_TABLE_SIZE = 2;
    public const int IGZIP_DECODE_OFFSET = 0;
#endif

    public const int IGZIP_LEN_TABLE_SIZE = 256;
    public const int IGZIP_LIT_TABLE_SIZE = DEF_LIT_SYMBOLS;
    public const int IGZIP_HUFFTABLE_CUSTOM = 0;
    public const int IGZIP_HUFFTABLE_DEFAULT = 1;
    public const int IGZIP_HUFFTABLE_STATIC = 2;

    public enum FlushFlags
    {
        NoFlush = 0, // Default
        SyncFlush = 1,
        FullFlush = 2,
        FinishFlush = 0, // Deprecated
    }

    // Gzip Flags
    public enum GzipFlags
    {
        Deflate = 0, // Default
        Gzip = 1,
        GzipNoHeader = 2,
        Zlib = 3,
        ZlibNoHeader = 4
    }

    /******************************************************************************/
    /* Inflate Implementation Specific Defines */
    /******************************************************************************/
    const int DECODE_LONG_BITS = 12;
    const int DECODE_SHORT_BITS = 10;

    /* In the following defines, L stands for LARGE and S for SMALL */
    const int L_REM = (21 - DECODE_LONG_BITS);
    const int S_REM = (15 - DECODE_SHORT_BITS);

    const int L_DUP = ((1 << L_REM) - (L_REM + 1));
    const int S_DUP = ((1 << S_REM) - (S_REM + 1));

    private const int L_UNUSED =
        ((1 << L_REM) - (1 << ((L_REM) / 2)) - (1 << ((L_REM + 1) / 2)) + 1);
    private const int S_UNUSED =
        ((1 << S_REM) - (1 << ((S_REM) / 2)) - (1 << ((S_REM + 1) / 2)) + 1);


    const int L_SIZE = (DEF_LIT_LEN_SYMBOLS + L_DUP + L_UNUSED);
    const int S_SIZE = (DEF_DIST_SYMBOLS + S_DUP + S_UNUSED);

    const int HUFF_CODE_LARGE_LONG_ALIGNED = (L_SIZE + (-L_SIZE & 0xf));
    const int HUFF_CODE_SMALL_LONG_ALIGNED = (S_SIZE + (-S_SIZE & 0xf));

    const int ISAL_DEF_MAX_HDR_SIZE = 328;
    const int ISAL_DEF_HIST_SIZE = 32 * IGZIP_K;
    const int ISAL_LOOK_AHEAD = (DEF_MAX_MATCH + 31) & ~31;

    /** @brief Large lookup table for decoding huffman codes */
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct inflate_huff_code_large
    {
        fixed uint short_code_lookup[1 << (DECODE_LONG_BITS)]; //!< Short code lookup table
        fixed ushort long_code_lookup[HUFF_CODE_LARGE_LONG_ALIGNED]; //!< Long code lookup table
    };

    /** @brief Small lookup table for decoding huffman codes */
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct inflate_huff_code_small
    {
        fixed ushort short_code_lookup[1 << (DECODE_SHORT_BITS)]; //!< Short code lookup table
        fixed ushort long_code_lookup[HUFF_CODE_SMALL_LONG_ALIGNED]; //!< Long code lookup table
    };

    /* Current state of decompression */
    enum block_state
    {
        BLOCK_NEW_HDR, /* Just starting a new block */
        BLOCK_HDR, /* In the middle of reading in a block header */
        BLOCK_TYPE0, /* Decoding a type 0 block */
        BLOCK_CODED, /* Decoding a huffman coded block */
        BLOCK_INPUT_DONE, /* Decompression of input is completed */
        BLOCK_FINISH, /* Decompression of input is completed and all data has been flushed to
                              output */
        GZIP_EXTRA_LEN,
        GZIP_EXTRA,
        GZIP_NAME,
        GZIP_COMMENT,
        GZIP_HCRC,
        ZLIB_DICT,
        CHECKSUM_CHECK,
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct InflateStateStart
    {
        public unsafe byte* next_out;
        public int avail_out;
        public int total_out;
        public unsafe byte* next_in;
        public ulong read_in;
        public int avail_in;
        int read_in_length; //!< Bits in read_in
        inflate_huff_code_large lit_huff_code; //!< Structure for decoding lit/len symbols

        inflate_huff_code_small dist_huff_code; //!< Structure for decoding dist symbols

        block_state block_state; //!< Current decompression state

        uint dict_length; //!< Length of dictionary used
        uint bfinal; //!< Flag identifying final block
        public GzipFlags crc_flag; //!< Flag identifying whether to track of crc
        public uint crc; //!< Contains crc or adler32 of output if crc_flag is set
        uint HistBits; // Log base 2 of maximum lookback distance
        /*
        union {
                   int32_t type0_block_len; //!< Length left to read of type 0 block when outbuffer
                                            //!< overflow occurred
                   int32_t count;           //!< Count of bytes remaining to be parsed
                   uint32_t dict_id;
           };
         */
        int Type0BlockLen; // Length left to read of type 0 block when outbuffer overflow occurred
        int WriteOverflowLits;
        int WriteOverflowLen;
        int CopyOverflowLength; // Length left to copy when outbuffer overflow occurred
        int CopyOverflowDistance; // Lookback distance when outbuffer overflow occurred
        short WrapperFlag;
        short TmpInSize; // Number of bytes in tmp_in_buffer
        int TmpOutValid; // Number of bytes in tmp_out_buffer
        int TmpOutProcessed; // Number of bytes processed in tmp_out_buffer
        unsafe fixed byte TmpInBuffer[ISAL_DEF_MAX_HDR_SIZE]; // Temporary buffer containing data from the input stream
        unsafe fixed byte TmpOutBuffer[2 * ISAL_DEF_HIST_SIZE + ISAL_LOOK_AHEAD]; // Temporary buffer containing data from the output stream
    }

    private static string? _realLibraryName;

    static IGZipBase()
    {
        NativeLibrary.SetDllImportResolver(typeof(IGZipBase).Assembly, ResolveUnmanagedDll);
    }

    private static IntPtr ResolveUnmanagedDll(string libraryName, Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        searchPath ??= DllImportSearchPath.SafeDirectories;
        var rc = NativeLibrary.TryLoad(_realLibraryName ?? libraryName, assembly, searchPath, out var handle);
        if (rc) return handle;
        FillNativeName();
        rc = NativeLibrary.TryLoad(_realLibraryName, assembly, searchPath, out handle);
        Console.WriteLine($"<DLL> ResolveUnmanagedDll {_realLibraryName} {rc}");
        return handle;
    }

    private static void FillNativeName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _realLibraryName = "libisal.so.2";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _realLibraryName = "libisal.dylib";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _realLibraryName = "isa-l.dll";
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "isal_inflate_init")]
    public static extern unsafe int InflateInit(void* state);

    [DllImport(NativeLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "isal_inflate")]
    public static extern unsafe int Inflate(void* state);
}
