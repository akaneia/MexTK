using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MexTK.FighterFunction
{
    public class Compiling
    {
        private static string StandardPath = @"C:/devkitpro/devkitPPC/bin/powerpc-eabi-gcc.exe";
        public static List<RelocELF> Compile(string[] inputs, bool disableWarnings, bool clean, int optimizationLevel = 2)
        {
            if (inputs.Length == 0)
                return null;

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new Exception("Invalid platform " + Environment.OSVersion.Platform.ToString());

            var devkitpath = Environment.GetEnvironmentVariable("DEVKITPPC").Replace("/opt/", "C:/");

            if (string.IsNullOrEmpty(devkitpath))
                throw new FileNotFoundException("DEVKITPPC path not set");

            var gccPath = Path.Combine(devkitpath, "bin/powerpc-eabi-gcc.exe");

            if (!File.Exists(gccPath))
                gccPath = gccPath.Replace("C:/", "");

            if (File.Exists(StandardPath))
                gccPath = StandardPath;

            if (!File.Exists(gccPath))
                throw new FileNotFoundException("powerpc-eabi-gcc.exe not found at " + gccPath);

            var ext = Path.GetExtension(inputs[0]).ToLower();
            List<RelocELF> elfs = new List<RelocELF>();

            foreach (var input in inputs)
            {
                if (ext.Equals(".a"))
                    elfs.AddRange(LibArchive.GetElfs(input));

                if (ext.Equals(".o"))
                    elfs.Add(new RelocELF(File.ReadAllBytes(input)));

                if (ext.Equals(".c") || ext.Equals(".cpp"))
                {
                    using (Process p = new Process())
                    {
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.WorkingDirectory = Path.GetDirectoryName(input);
                        p.StartInfo.RedirectStandardOutput = true;
                        p.StartInfo.FileName = gccPath;
                        // add -g for debug symbols
                        p.StartInfo.Arguments = $"-MMD -MP -Wall -DGEKKO -mogc -mcpu=750 -meabi -mno-longcall -mhard-float -fpermissive -g -c \"{input}\" {(disableWarnings ? "-w" : "")} -O{optimizationLevel}";
                        p.Start();

                        p.WaitForExit();

                        var outputPath = Path.Combine(Path.GetDirectoryName(input), Path.GetFileNameWithoutExtension(input) + ".o");
                        var outputPathD = Path.Combine(Path.GetDirectoryName(input), Path.GetFileNameWithoutExtension(input) + ".d");

                        if (p.ExitCode != 0 || !File.Exists(outputPath))
                        {
                            Console.WriteLine();
                            throw new InvalidDataException($"{Path.GetFileName(input)} failed to compile, see output above for details");
                        }

                        elfs.Add(new RelocELF(File.ReadAllBytes(outputPath)));

                        if (clean)
                        {
                            File.Delete(outputPath);
                            File.Delete(outputPathD);
                        }
                    }
                }
            }

            return elfs;
        }
    }
}
