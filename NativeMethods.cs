using System.IO;
using System.Runtime.InteropServices;

namespace ICOforge
{
    internal static class NativeMethods
    {
        private static readonly Guid DownloadsFolderGuid = new("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

        public static string GetDownloadsPath()
        {
            try
            {
                if (Environment.OSVersion.Version.Major < 6) throw new NotSupportedException();

                SHGetKnownFolderPath(DownloadsFolderGuid, 0, IntPtr.Zero, out IntPtr pathPtr);

                try
                {
                    return Marshal.PtrToStringUni(pathPtr) ?? GetDefaultUserProfilePath();
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathPtr);
                }
            }
            catch
            {
                return GetDefaultUserProfilePath();
            }
        }

        private static string GetDefaultUserProfilePath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
}