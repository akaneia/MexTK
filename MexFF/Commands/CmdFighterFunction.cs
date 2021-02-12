using HSDRaw;
using MexTK.FighterFunction;
using MexTK.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace MexTK.Commands
{
    public class CmdFighterFunction : ICommand
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "DAT Fighter/Item/Stage Function Table";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ID()
        {
            return "-ff";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Help()
        {
            return @"Usage: MexTK.exe -ff (options)
Options:
    -i (file.c file2.c ...)               : Input File (.c and .o (elf)) files are supported can have multiple inputs for 'it' type
    -item (item index) (file.c)           : specify index used for building ftItem tables
    -o (file.dat)                         : output dat filename
    -dat (file.dat)                       : dat file to inject symbol into
    -s (symbol)                           : symbol name (default is ftFunction for ft and itFunction for it)
    -t (file.txt)                         : specify symbol table list
    -op (1-3)                             : optimization level
    -ow                                   : automatically overwrite files if they exists
    -w                                    : show compilation warnings
    -v                                    : Verbose Mode (Console prints more information)
    -d                                    : debug build (.o files are untouched after compilation)
    -l (file.lst)                         : specify external link file)

Ex: mexff.exe -i 'main.c' -o 'ftFunction.dat' -q
Compile 'main.c' and outputs 'ftFunction.dat' in quiet mode

Ex: mexff.exe -item 0 'bomb.c' -item 3 'arrow.c' -d 'PlMan.dat' -s 'itFunction'
Creates function table with bomb.c with index 0 and arrow.c with index 3
and injects it into PlMan.dat with the symbol name itFunction";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool DoIt(string[] args)
        {
            // Parse Args
            List<Tuple<int, string>> itemInputs = new List<Tuple<int, string>>();
            List<string> inputs = new List<string>();
            LinkFile linkFile = new LinkFile();
            string output = "";
            string symbolName = "";
            string datFile = null;
            string[] fightFuncTable = null;
            bool quiet = true;
            bool yesOverwrite = false;
            bool disableWarnings = true;
            bool clean = true;
            int opLevel = 2;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-i")
                {
                    for (int j = i + 1; j < args.Length; j++)
                        if (File.Exists(args[j]))
                            inputs.Add(Path.GetFullPath(args[j]));
                        else
                            break;
                }

                if (args[i] == "-item" && i + 2 < args.Length)
                    itemInputs.Add(new Tuple<int, string>(int.Parse(args[i + 1]), Path.GetFullPath(args[i + 2])));

                if (args[i] == "-op" && i + 1 < args.Length)
                    opLevel = int.Parse(args[i + 1]);

                if (args[i] == "-o" && i + 1 < args.Length)
                    output = Path.GetFullPath(args[i + 1]);

                if (args[i] == "-dat" && i + 1 < args.Length)
                    datFile = Path.GetFullPath(args[i + 1]);

                if (args[i] == "-s" && i + 1 < args.Length)
                    symbolName = args[i + 1];

                if (args[i] == "-ow")
                    yesOverwrite = true;

                if (args[i] == "-w")
                    disableWarnings = false;

                if (args[i] == "-d")
                    clean = false;

                if (args[i] == "-t" && i + 1 < args.Length)
                    fightFuncTable = File.ReadAllLines(args[i + 1]);

                if (args[i] == "-v")
                    quiet = false;

                if (args[i] == "-l" && i + 1 < args.Length)
                    linkFile.LoadLinkFile(args[i + 1]);
            }

            //
            if(string.IsNullOrEmpty(symbolName))
            {
                Console.WriteLine("Error: Symbol Required; please specify a symbol Ex:\"-s ftFunction\"");
                return false;
            }

            // if output is null set the name to the symbol name
            if (string.IsNullOrEmpty(datFile) && 
                string.IsNullOrEmpty(output) && 
                !string.IsNullOrEmpty(symbolName))
                output = symbolName + ".dat";

            var symbolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, symbolName + ".txt");

            // if function table not specified attempt to get it from symbol name
            if (fightFuncTable == null && 
                !string.IsNullOrEmpty(symbolName) &&
                File.Exists(symbolPath))
            {
                fightFuncTable = File.ReadAllLines(symbolPath);
            }

            // load link file in mex directory
            foreach (var f in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory))
            {
                if (Path.GetExtension(f).ToLower().Equals(".link"))
                    linkFile.LoadLinkFile(f);
            }

            // don't allow both item tables and normal tables
            if (inputs.Count > 0 && itemInputs.Count > 0)
            {
                Console.WriteLine("Only -i or -ii can be used at once, not both");
                return false;
            }

            // print instruction and exit if no input
            if ((inputs.Count == 0
                 && itemInputs.Count == 0)
                || args.Length == 0)
            {
                return false;
            }

            // check if symbol name is given
            if (string.IsNullOrEmpty(symbolName))
            {
                symbolName = "ftFunction";
                Console.WriteLine("No symbol name given, defaulting to \"ftFunction\"");
            }

            // create output path if one isn't entered
            if (string.IsNullOrEmpty(output) && string.IsNullOrEmpty(datFile))
                output = Path.Combine(Path.GetDirectoryName(inputs[0]), Path.GetFileName(inputs[0]).Replace(Path.GetExtension(inputs[0]), ".dat"));

            // check if output already exists
            if (File.Exists(output) && !yesOverwrite)
            {
                Console.WriteLine(output + " already exists, overwrite? (y/n)");
                if (Console.ReadLine().Trim().ToLower() != "y")
                    return false;
            }

            // compile functions
            HSDAccessor function = null;

            if (itemInputs.Count == 0)
                function = CompileInput(inputs.ToArray(), fightFuncTable, linkFile, quiet, disableWarnings, clean, opLevel);
            else
            {
                function = new HSDAccessor() { _s = new HSDStruct(4) };

                int count = 0;
                foreach (var f in itemInputs)
                {
                    count = Math.Max(count, f.Item1 + 1);

                    if (4 + 4 * (f.Item1 + 1) > function._s.Length)
                        function._s.Resize(4 + 4 * (f.Item1 + 1));

                    var relocFunc = CompileInput(new string[] { f.Item2 }, fightFuncTable, linkFile, quiet, disableWarnings, clean, opLevel);
                    function._s.SetReference(4 + 0x04 * f.Item1, relocFunc);
                }
                function._s.SetInt32(0x00, count);
            }

            // create DAT file
            if (function != null)
            {
                // save new file
                if (!string.IsNullOrEmpty(output))
                {
                    var f = new HSDRawFile();
                    DatTools.InjectSymbolIntoDat(f, symbolName, function);
                    f.Save(output);
                    Console.WriteLine("saving " + output + "...");
                }

                if (!string.IsNullOrEmpty(datFile))
                {
                    var f = File.Exists(datFile) ? new HSDRawFile(datFile) : new HSDRawFile();
                    DatTools.InjectSymbolIntoDat(f, symbolName, function);
                    f.Save(datFile);
                    Console.WriteLine("saving " + datFile + "...");
                }

                // We did it boys
                Console.WriteLine();
                Console.WriteLine("Sucessfully Compiled and Converted to DAT!");

                return true;
            }

            return false;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="fightFuncTable"></param>
        /// <param name="quiet"></param>
        /// <returns></returns>
        private static HSDAccessor CompileInput(string[] inputs, string[] funcTable, LinkFile link, bool quiet, bool disableWarnings, bool clean, int optimizationLevel = 2)
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

            if (!File.Exists(gccPath))
                throw new FileNotFoundException("powerpc-eabi-gcc.exe not found at " + gccPath);

            var ext = Path.GetExtension(inputs[0]).ToLower();
            List<RelocELF> elfs = new List<RelocELF>();

            foreach (var input in inputs)
            {
                if (ext.Equals(".o"))
                    elfs.Add(new RelocELF(File.ReadAllBytes(input)));

                if (ext.Equals(".c"))
                {
                    Process p = new Process();

                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.WorkingDirectory = Path.GetDirectoryName(input);
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.FileName = gccPath;
                    p.StartInfo.Arguments = $"-MMD -MP -Wall -DGEKKO -mogc -mcpu=750 -meabi -mno-longcall -mhard-float -c \"{input}\" {(disableWarnings ? "-w" : "")} -O{optimizationLevel}";
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

            return RelocELF.GenerateFunctionDAT(elfs.ToArray(), link, funcTable, quiet);
        }


    }
}
