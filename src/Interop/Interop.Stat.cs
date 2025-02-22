// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Nanook.GrindCore
{
#if !NET7_0_OR_GREATER
    [Flags]
    internal enum UnixFileMode : uint
    {
        UserRead = 0x0100,    // owner has read permission
        UserWrite = 0x0080,   // owner has write permission
        UserExecute = 0x0040, // owner has execute permission
        GroupRead = 0x0020,   // group has read permission
        GroupWrite = 0x0010,  // group has write permission
        GroupExecute = 0x0008, // group has execute permission
        OtherRead = 0x0004,   // others have read permission
        OtherWrite = 0x0002,  // others have write permission
        OtherExecute = 0x0001, // others have execute permission
    }
#endif

    internal static partial class Interop
    {
        internal static partial class Sys
        {
            // Even though csc will by default use a sequential layout, a CS0649 warning as error
            // is produced for un-assigned fields when no StructLayout is specified.
            //
            // Explicitly saying Sequential disables that warning/error for consumers which only
            // use Stat in debug builds.
            [StructLayout(LayoutKind.Sequential)]
            internal struct FileStatus
            {
                internal FileStatusFlags Flags;
                internal int Mode;
                internal uint Uid;
                internal uint Gid;
                internal long Size;
                internal long ATime;
                internal long ATimeNsec;
                internal long MTime;
                internal long MTimeNsec;
                internal long CTime;
                internal long CTimeNsec;
                internal long BirthTime;
                internal long BirthTimeNsec;
                internal long Dev;
                internal long RDev;
                internal long Ino;
                internal uint UserFlags;
            }

            internal static class FileTypes
            {
                internal const int S_IFMT = 0xF000;
                internal const int S_IFIFO = 0x1000;
                internal const int S_IFCHR = 0x2000;
                internal const int S_IFDIR = 0x4000;
                internal const int S_IFBLK = 0x6000;
                internal const int S_IFREG = 0x8000;
                internal const int S_IFLNK = 0xA000;
                internal const int S_IFSOCK = 0xC000;
            }

            [Flags]
            internal enum FileStatusFlags
            {
                None = 0,
                HasBirthTime = 1,
            }

            //[LibraryImport(Libraries.SystemNative, EntryPoint = "SystemNative_FStat", SetLastError = true)]
            //internal static partial int FStat(SafeHandle fd, out FileStatus output);

            //[LibraryImport(Libraries.SystemNative, EntryPoint = "SystemNative_Stat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
            //internal static partial int Stat(string path, out FileStatus output);

            //[LibraryImport(Libraries.SystemNative, EntryPoint = "SystemNative_LStat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
            //internal static partial int LStat(string path, out FileStatus output);
        }
    }
}