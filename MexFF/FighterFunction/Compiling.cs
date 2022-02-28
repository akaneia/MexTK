using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MexTK.FighterFunction
{
    public class Compiling
    {
        private static string StandardPath = @"C:/devkitpro/devkitPPC/";

        public static List<RelocELF> Compile(string[] inputs, bool disableWarnings, bool clean, int optimizationLevel = 2, string[] includes = null, string buildPath = null, bool debugSymbols = false, bool quiet = true)
        {
            bool isWin32 = Environment.OSVersion.Platform == PlatformID.Win32NT;
            if (inputs.Length == 0)
                return null;

            var devkitpath = isWin32 ? StandardPath : Environment.GetEnvironmentVariable("DEVKITPPC");
            var gccPath = Path.Combine(devkitpath, "bin/powerpc-eabi-gcc");
            var gppPath = Path.Combine(devkitpath, "bin/powerpc-eabi-g++");
            
            buildPath = buildPath ?? Path.Combine(Path.GetDirectoryName(inputs[0]), "build");

            var includeList = new List<String>();
            // Add build path as include
            includeList.Add(buildPath); 
            if (includes != null) includeList.AddRange(includes);

            if (!Directory.Exists(buildPath)) Directory.CreateDirectory(buildPath);
            
            if (isWin32)
            {
                devkitpath = devkitpath.Replace("/opt/", "C:/");

                if (string.IsNullOrEmpty(devkitpath))
                    throw new FileNotFoundException("DEVKITPPC path not set");

                gccPath = Path.Combine(gccPath, ".exe");

                if (!File.Exists(gccPath))
                    gccPath = gccPath.Replace("C:/", "");

                if (!File.Exists(gccPath))
                    throw new FileNotFoundException("powerpc-eabi-gcc bin not found at " + gccPath);
                
                if (!File.Exists(gppPath))
                    throw new FileNotFoundException("powerpc-eabi-g++ bin not found at " + gppPath);

            }
                
            List<RelocELF> elfs = new List<RelocELF>();

            // Not used right now but if there's a cpp file, then the linker should be g++
            // since MexTK is it's own linker, not sure how this would affect at all.  
            bool isCppProject = false;          
            
            foreach (var input in inputs)
            {
                var ext = Path.GetExtension(input).ToLower();
                var isCpp = ext.Equals(".cpp");
                
                isCppProject = isCppProject || isCpp;

                if (ext.Equals(".a"))
                    elfs.AddRange(LibArchive.GetElfs(input));

                if (ext.Equals(".o"))
                    elfs.Add(new RelocELF(File.ReadAllBytes(input)));

                if (ext.Equals(".c") || isCpp)
                {
                    using (Process p = new Process())
                    {
                        var outputPath = Path.Combine(buildPath, Path.GetFileNameWithoutExtension(input) + ".o");
                        var outputPathD = Path.Combine(buildPath, Path.GetFileNameWithoutExtension(input) + ".d");
                        
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.WorkingDirectory = Path.GetDirectoryName(input);
                        p.StartInfo.RedirectStandardOutput = true;
                        // Use g++ or gcc as needed
                        p.StartInfo.FileName = isCpp ? gppPath : gccPath;
                        
                        var includesStr = "";
                        if (includeList.Count > 0)
                        {
                            includesStr = $"-I{String.Join(" -I", includeList.ToArray())}";
                        }
                        
                        // add -g for debug symbols
                        p.StartInfo.Arguments = $"-MMD -MP -MF \"{outputPathD}\" {(debugSymbols ? "-g" : "")} {(disableWarnings ? "-w" : "")} -O{optimizationLevel} -Wall -DGEKKO -mogc -mcpu=750 -meabi -mhard-float   {includesStr} -c \"{input}\" -o \"{outputPath}\" -fpermissive";

                        if (!quiet)
                        {
                            Console.WriteLine($"{p.StartInfo.FileName} {p.StartInfo.Arguments}");
                        }
                        p.Start();

                        p.WaitForExit();

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
