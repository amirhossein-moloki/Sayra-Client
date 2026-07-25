using System;
using System.Runtime.InteropServices;

namespace Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Tokens
{
    public class SafeTokenHandle : SafeHandle
    {
        public SafeTokenHandle() : base(IntPtr.Zero, true) { }

        public SafeTokenHandle(IntPtr handle) : base(IntPtr.Zero, true)
        {
            SetHandle(handle);
        }

        public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                return CloseHandle(handle);
            }
            return true;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
