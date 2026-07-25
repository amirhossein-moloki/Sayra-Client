using System;
using System.Runtime.InteropServices;

namespace Sayra.Client.Shared.Security.Memory;

public static class MemoryProtector
{
    private const uint CRYPTPROTECTMEMORY_SAME_PROCESS = 0x00;
    private const int BLOCK_SIZE = 16;

    public static bool Protect(IntPtr pointer, int size)
    {
        if (pointer == IntPtr.Zero || size <= 0) return false;

        // CryptProtectMemory requires size to be multiple of 16 bytes
        if (size % BLOCK_SIZE != 0)
        {
            // Fallback to simple XOR obfuscation if size is not a multiple of 16
            XorObfuscate(pointer, size);
            return true;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return NativeMethods.CryptProtectMemory(pointer, (uint)size, CRYPTPROTECTMEMORY_SAME_PROCESS);
            }
            catch
            {
                // Fallback on failure
            }
        }

        XorObfuscate(pointer, size);
        return true;
    }

    public static bool Unprotect(IntPtr pointer, int size)
    {
        if (pointer == IntPtr.Zero || size <= 0) return false;

        if (size % BLOCK_SIZE != 0)
        {
            XorObfuscate(pointer, size);
            return true;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return NativeMethods.CryptUnprotectMemory(pointer, (uint)size, CRYPTPROTECTMEMORY_SAME_PROCESS);
            }
            catch
            {
                // Fallback on failure
            }
        }

        XorObfuscate(pointer, size);
        return true;
    }

    private static void XorObfuscate(IntPtr pointer, int size)
    {
        // Simple fast XOR with a fixed/dynamic internal pattern for non-Windows or non-block sizes
        unsafe
        {
            byte* ptr = (byte*)pointer.ToPointer();
            for (int i = 0; i < size; i++)
            {
                ptr[i] ^= (byte)(0x5A ^ (i % 256));
            }
        }
    }

    private static class NativeMethods
    {
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CryptProtectMemory(IntPtr pData, uint cbData, uint dwFlags);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CryptUnprotectMemory(IntPtr pData, uint cbData, uint dwFlags);
    }
}
