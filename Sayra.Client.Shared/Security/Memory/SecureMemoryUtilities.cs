using System;
using System.Security.Cryptography;
using System.Threading;

namespace Sayra.Client.Shared.Security.Memory;

public static class SecureMemoryUtilities
{
    public static void SecureZero(byte[]? array)
    {
        if (array == null) return;
        for (int i = 0; i < array.Length; i++)
        {
            Volatile.Write(ref array[i], 0);
        }
    }

    public static void SecureZero(Span<byte> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            Volatile.Write(ref span[i], 0);
        }
    }

    public static byte[] GenerateSecureRandomBytes(int length)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        byte[] bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return bytes;
    }
}
