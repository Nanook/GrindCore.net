
using System.IO;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore.Zip
{
    internal static partial class ZipArchiveEntryConstants
    {
        static ZipArchiveEntryConstants()
        {
            DefaultFileExternalAttributes = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0 :
            (Interop.Sys.FileTypes.S_IFREG |
                (uint)(UnixFileMode.UserRead | UnixFileMode.UserWrite
                    | UnixFileMode.GroupRead
                    | UnixFileMode.OtherRead)
            ) << 16;

            DefaultDirectoryExternalAttributes = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0 :
            (Interop.Sys.FileTypes.S_IFDIR |
                (uint)(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute)
            ) << 16;
        }
        /// <summary>
        /// The default external file attributes are used to support zip archives on multiple platforms.
        /// </summary>
        internal static readonly uint DefaultFileExternalAttributes;

        /// <summary>
        /// The default external directory attributes are used to support zip archives on multiple platforms.
        /// Directories on Unix require the execute permissions to get into them.
        /// </summary>
        internal static readonly uint DefaultDirectoryExternalAttributes;
    }
}
