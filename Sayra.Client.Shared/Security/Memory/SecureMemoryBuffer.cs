using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sayra.Client.Shared.Security.Memory;

public delegate void ReadOnlySpanAction(ReadOnlySpan<byte> span);

public sealed class SecureMemoryBuffer : IDisposable
{
    private IntPtr _bufferPointer;
    private readonly int _length;
    private bool _isDisposed;
    private readonly object _lock = new();

    public int Length => _length;

    public SecureMemoryBuffer(int length)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        _length = length;

        // Allocate unmanaged memory
        _bufferPointer = Marshal.AllocHGlobal(length);

        // Zero it immediately after allocation
        ZeroBuffer();

        // Attempt to lock memory (VirtualLock)
        LockBuffer();
    }

    public void Write(byte[] source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        Write(source.AsSpan());
    }

    public void Write(ReadOnlySpan<byte> source)
    {
        lock (_lock)
        {
            CheckDisposed();
            if (source.Length > _length)
                throw new ArgumentException("Source data exceeds buffer length.", nameof(source));

            unsafe
            {
                byte* destPtr = (byte*)_bufferPointer.ToPointer();
                for (int i = 0; i < source.Length; i++)
                {
                    destPtr[i] = source[i];
                }
            }
        }
    }

    public void Read(byte[] destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        Read(destination.AsSpan());
    }

    public void Read(Span<byte> destination)
    {
        lock (_lock)
        {
            CheckDisposed();
            if (destination.Length < _length)
                throw new ArgumentException("Destination span is too small.", nameof(destination));

            unsafe
            {
                byte* srcPtr = (byte*)_bufferPointer.ToPointer();
                for (int i = 0; i < _length; i++)
                {
                    destination[i] = srcPtr[i];
                }
            }
        }
    }

    public void UseBuffer(ReadOnlySpanAction action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        lock (_lock)
        {
            CheckDisposed();
            unsafe
            {
                var span = new ReadOnlySpan<byte>(_bufferPointer.ToPointer(), _length);
                action(span);
            }
        }
    }

    private void LockBuffer()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                bool success = NativeMethods.VirtualLock(_bufferPointer, (UIntPtr)_length);
                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine($"VirtualLock failed: {Marshal.GetLastWin32Error()}");
                }
            }
            catch
            {
                // Fallback for platform issues or permissions
            }
        }
    }

    private void UnlockBuffer()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _bufferPointer != IntPtr.Zero)
        {
            try
            {
                NativeMethods.VirtualUnlock(_bufferPointer, (UIntPtr)_length);
            }
            catch
            {
                // Safe ignore
            }
        }
    }

    private void ZeroBuffer()
    {
        if (_bufferPointer == IntPtr.Zero) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                NativeMethods.RtlZeroMemory(_bufferPointer, (IntPtr)_length);
                return;
            }
            catch
            {
                // Fallback to managed zeroing if P/Invoke fails
            }
        }

        // Managed Zeroing with volatile write barrier to prevent compiler optimizing it out
        unsafe
        {
            byte* ptr = (byte*)_bufferPointer.ToPointer();
            for (int i = 0; i < _length; i++)
            {
                Volatile.Write(ref ptr[i], 0);
            }
        }
    }

    private void CheckDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(SecureMemoryBuffer));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            ZeroBuffer();
            UnlockBuffer();

            if (_bufferPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_bufferPointer);
                _bufferPointer = IntPtr.Zero;
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~SecureMemoryBuffer()
    {
        Dispose();
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualLock(IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualUnlock(IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
        public static extern void RtlZeroMemory(IntPtr dest, IntPtr size);
    }
}
