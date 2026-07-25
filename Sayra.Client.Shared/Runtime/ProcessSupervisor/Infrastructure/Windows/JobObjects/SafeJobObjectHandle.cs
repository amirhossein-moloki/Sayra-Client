using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.JobObjects
{
    public class SafeJobObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobObjectHandle() : base(true) { }

        public SafeJobObjectHandle(IntPtr handle, bool ownsHandle) : base(ownsHandle)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            {
                return CloseHandle(handle);
            }
            return true;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
