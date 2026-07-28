using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Integrates with the native Windows Restart Manager to handle process locking and graceful app restarting.
    /// Provides platform-agnostic fallback for testing on Linux/non-Windows environments.
    /// </summary>
    public class WindowsRestartManager : IRestartManagerService
    {
        private const int CCH_RM_SESSION_KEY = 32;
        private const int CCH_RM_MAX_APP_NAME = 255;
        private const int CCH_RM_MAX_SVC_NAME = 63;

        private uint? _activeSessionHandle;
        private string? _activeSessionKey;

        #region Native Windows P/Invokes

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public uint dwProcessId;
            public FILETIME ProcessStartTime;
        }

        private enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;
            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bGracefulShutdownRequired;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, uint dwSessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(
            uint pSessionHandle,
            uint nFiles,
            string[]? rgsFilenames,
            uint nApplications,
            RM_UNIQUE_PROCESS[]? rgApplications,
            uint nServices,
            string[]? rgsServiceNames);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmGetList(
            uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
            out uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmShutdown(uint pSessionHandle, uint lActionFlags, IntPtr fnStatus);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmRestart(uint pSessionHandle, uint dwRestartFlags, IntPtr fnStatus);

        #endregion

        /// <inheritdoc />
        public List<LockingProcess> DetectFileLocks(IEnumerable<string> paths)
        {
            var lockingProcesses = new List<LockingProcess>();

            if (paths == null)
            {
                return lockingProcesses;
            }

            if (!OperatingSystem.IsWindows())
            {
                // Platform fallback for non-Windows (CI testing)
                // We simulate an empty list or mock detections
                return lockingProcesses;
            }

            StringBuilder sessionKey = new StringBuilder(CCH_RM_SESSION_KEY + 1);
            int startResult = RmStartSession(out uint sessionHandle, 0, sessionKey);
            if (startResult != 0)
            {
                throw new RestartManagerException("Failed to start Restart Manager session.", startResult);
            }

            try
            {
                var fileList = new List<string>();
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        fileList.Add(Path.GetFullPath(path));
                    }
                }

                if (fileList.Count == 0)
                {
                    return lockingProcesses;
                }

                string[] filePathsArray = fileList.ToArray();
                int registerResult = RmRegisterResources(sessionHandle, (uint)filePathsArray.Length, filePathsArray, 0, null, 0, null);
                if (registerResult != 0)
                {
                    throw new RestartManagerException("Failed to register resources to Restart Manager.", registerResult);
                }

                uint pnProcInfoNeeded = 0;
                uint pnProcInfo = 0;
                uint lpdwRebootReasons = 0;

                // Call RmGetList once to find out how many process info elements we need to allocate
                int getListResult = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null, out lpdwRebootReasons);
                if (getListResult != 0 && getListResult != 234) // 234 = ERROR_MORE_DATA
                {
                    throw new RestartManagerException("Failed to retrieve locking processes count from Restart Manager.", getListResult);
                }

                if (pnProcInfoNeeded > 0)
                {
                    pnProcInfo = pnProcInfoNeeded;
                    var processInfos = new RM_PROCESS_INFO[pnProcInfo];
                    getListResult = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfos, out lpdwRebootReasons);
                    if (getListResult != 0)
                    {
                        throw new RestartManagerException("Failed to retrieve locking processes list from Restart Manager.", getListResult);
                    }

                    for (int i = 0; i < pnProcInfo; i++)
                    {
                        lockingProcesses.Add(new LockingProcess
                        {
                            ProcessId = processInfos[i].Process.dwProcessId,
                            AppName = processInfos[i].strAppName,
                            ServiceShortName = processInfos[i].strServiceShortName
                        });
                    }
                }
            }
            finally
            {
                RmEndSession(sessionHandle);
            }

            return lockingProcesses;
        }

        /// <inheritdoc />
        public bool ShutdownApplications(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return true;
            }

            if (!OperatingSystem.IsWindows())
            {
                // Platform fallback for non-Windows (CI testing)
                return true;
            }

            StringBuilder sessionKey = new StringBuilder(CCH_RM_SESSION_KEY + 1);
            int startResult = RmStartSession(out uint sessionHandle, 0, sessionKey);
            if (startResult != 0)
            {
                throw new RestartManagerException("Failed to start Restart Manager session.", startResult);
            }

            _activeSessionHandle = sessionHandle;
            _activeSessionKey = sessionKey.ToString();

            try
            {
                var fileList = new List<string>();
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        fileList.Add(Path.GetFullPath(path));
                    }
                }

                if (fileList.Count == 0)
                {
                    return true;
                }

                string[] filePathsArray = fileList.ToArray();
                int registerResult = RmRegisterResources(sessionHandle, (uint)filePathsArray.Length, filePathsArray, 0, null, 0, null);
                if (registerResult != 0)
                {
                    throw new RestartManagerException("Failed to register resources to Restart Manager.", registerResult);
                }

                // Call RmShutdown. dwActionFlags = 1 (RmForceShutdown)
                int shutdownResult = RmShutdown(sessionHandle, 1, IntPtr.Zero);
                if (shutdownResult != 0)
                {
                    throw new RestartManagerException("Failed to shut down locking applications via Restart Manager.", shutdownResult);
                }

                return true;
            }
            catch (Exception)
            {
                CleanSession();
                throw;
            }
        }

        /// <inheritdoc />
        public bool RestartApplications()
        {
            if (!OperatingSystem.IsWindows())
            {
                // Platform fallback for non-Windows (CI testing)
                return true;
            }

            if (!_activeSessionHandle.HasValue)
            {
                return true;
            }

            try
            {
                int restartResult = RmRestart(_activeSessionHandle.Value, 0, IntPtr.Zero);
                if (restartResult != 0)
                {
                    throw new RestartManagerException("Failed to restart applications via Restart Manager.", restartResult);
                }

                return true;
            }
            finally
            {
                CleanSession();
            }
        }

        private void CleanSession()
        {
            if (_activeSessionHandle.HasValue)
            {
                RmEndSession(_activeSessionHandle.Value);
                _activeSessionHandle = null;
                _activeSessionKey = null;
            }
        }
    }
}
