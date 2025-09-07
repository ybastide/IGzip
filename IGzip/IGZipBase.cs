using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;

namespace IGzip;

[SuppressUnmanagedCodeSecurity]
internal static class IGZipBase
{
    private const string NativeLib = "isal";

    internal const int InflateStateStructSize = 128 * 1024; // sizeof(inflate_state): 87368 AFAIK (Linux, Mac)

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

    [StructLayout(LayoutKind.Sequential)]
    internal struct InflateStateStart
    {
        public unsafe byte* next_out;
        public int avail_out;
        public int total_out;
        public unsafe byte* next_in;
        public ulong total_in;
        public int avail_in;
        // the other fields are not used
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
            _realLibraryName = "isal.dll";
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
