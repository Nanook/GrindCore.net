using Nanook.GrindCore;
using System.Runtime.InteropServices;
using System;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {

        internal static partial class Libraries
        {
            internal const string GrindCoreLib = "GrindCore";
        }

        public static class MultiplatformLoader
        {
            private static readonly HashSet<string> loadedLibraries = new HashSet<string>();
            private static readonly object lockObject = new object();

            public static void LoadLibrary(string libraryName)
            {
                lock (lockObject)
                {
                    if (loadedLibraries.Contains(libraryName))
                        return;

                    string libraryPath = GetLibraryPath(libraryName);

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        if (NativeMethodsWindows.LoadLibrary(libraryPath) == IntPtr.Zero)
                            throw new Exception($"Failed to load library: {libraryPath}");
                    }
                    else
                    {
                        if (NativeMethodsUnix.dlopen(libraryPath, NativeMethodsUnix.RTLD_NOW) == IntPtr.Zero)
                            throw new Exception($"Failed to load library: {libraryPath}");
                    }
                    loadedLibraries.Add(libraryName);
                }
            }

            public static string GetRuntimeIdentifier()
            {
                string os = "";
                string arch = "";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    os = "win";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    os = "linux";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    os = "osx";
                else
                    throw new PlatformNotSupportedException("Unsupported platform.");

                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X64:
                        arch = "x64";
                        break;
                    case Architecture.X86:
                        arch = "x86";
                        break;
                    case Architecture.Arm64:
                        arch = "arm64";
                        break;
                    case Architecture.Arm:
                        arch = "arm";
                        break;
                    default:
                        throw new PlatformNotSupportedException("Unsupported architecture.");
                }

                return $"{os}-{arch}";
            }

            private static string GetLibraryPath(string libraryName)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", GetRuntimeIdentifier(), "native", $"{libraryName}.dll");
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", GetRuntimeIdentifier(), "native", $"lib{libraryName}.so");
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", GetRuntimeIdentifier(), "native", $"lib{libraryName}.dylib");
                throw new PlatformNotSupportedException("Unsupported platform.");
            }

            private static class NativeMethodsWindows
            {
                [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
                public static extern IntPtr LoadLibrary(string lpFileName);
                [DllImport("kernel32", SetLastError = true)]
                public static extern bool FreeLibrary(IntPtr hModule);
            }

            private static class NativeMethodsUnix
            {
                [DllImport("libdl")]
                public static extern IntPtr dlopen(string filename, int flags);

                [DllImport("libdl")]
                public static extern IntPtr dlsym(IntPtr handle, string symbol);

                [DllImport("libdl")]
                public static extern int dlclose(IntPtr handle);

                public const int RTLD_NOW = 2;
            }
        }
    }

}