using System;
using System.Diagnostics;
using System.IO;
using Nanook.GrindCore;
using Xunit;

namespace GrindCore.Tests
{
    public sealed class Lzma2ExtractTest
    {
        [Fact]
        public void Extract_Ps3_Terraria_Japan_Iso_Lzma2()
        {
            const string inputPath = @"D:\Temp\Ps3_Terraria_Japan.iso.lzma2";

            if (!File.Exists(inputPath))
            {
                Trace.WriteLine($"Test data not present: {inputPath} - skipping");
                return; // skip when running on machines without the file
            }

            string outputPath = inputPath.EndsWith(".lzma2", StringComparison.OrdinalIgnoreCase)
                ? inputPath.Substring(0, inputPath.Length - ".lzma2".Length)
                : Path.ChangeExtension(inputPath, null);

            var options = new CompressionOptions
            {
                Type = CompressionType.Decompress,
                LeaveOpen = false,
                InitProperties = new byte[] { 0x18 }
            };

            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }

            using (var inFs = File.OpenRead(inputPath))
            using (var decStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, inFs, options))
            using (var outFs = File.Create(outputPath))
            {
                decStream.CopyTo(outFs);
                outFs.Flush();
            }

            var fi = new FileInfo(outputPath);
            Trace.WriteLine($"Decompressed output: {outputPath} ({fi.Length} bytes)");
            Assert.True(fi.Exists, "Output file was not created");
            Assert.True(fi.Length > 0, "Output file is empty");
        }
    }
}
