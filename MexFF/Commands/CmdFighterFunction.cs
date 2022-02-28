using HSDRaw;
using HSDRaw.Melee.Pl;
using MexTK.FighterFunction;
using MexTK.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using static MexTK.FighterFunction.RelocELF;

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
    -s (symbol)                           : symbol name (default is ftFunction for ft and itFunction for item)
    -t (file.txt)                         : specify symbol table list
    -op (1-3)                             : optimization level
    -ow                                   : automatically overwrite files if they exists
    -w                                    : show compilation warnings
    -v                                    : Verbose Mode (Console prints more information)
    -b (path/to/build)                    : path to output build (.o/.d files) files to.
    -inc (path/to/include/1) (path/2)     : specify libraries for compiler to include.
    -d                                    : debug symbols added to output
    -c                                    : deletes .o files after compilation
    -l (file.link)                        : specify external link file

Ex: mexff.exe -i 'main.c' -o 'ftFunction.dat' -q
Compile 'main.c' and outputs 'ftFunction.dat' in quiet mode

Ex: mexff.exe -item 0 'bomb.c' -item 3 'arrow.c' -s itFunction -dat 'PlFt.dat'
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
            List<Tuple<int, List<string>>> itemInputs = new List<Tuple<int, List<string>>>();
            List<string> inputs = new List<string>();
            LinkFile linkFile = new LinkFile();
            string output = "";
            string symbolName = "";
            string datFile = null;
            string[] fightFuncTable = null;
            string buildPath = null;
            bool quiet = true;
            bool yesOverwrite = false;
            bool disableWarnings = true;
            bool clean = false;
            bool debug = false;
            int opLevel = 2;
            List<string> includes = new List<string>();

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
                
                if (args[i] == "-inc")
                {
                    for (int j = i + 1; j < args.Length; j++)
                        if (Directory.Exists(args[j]))
                            includes.Add(Path.GetFullPath(args[j]));
                        else
                            break;
                }

                if (args[i] == "-item" && i + 2 < args.Length)
                {
                    List<string> itemRefList = new List<string>();

                    for (int j = i + 2; j < args.Length; j++)
                    {
                        if (args[j] != "-item" && File.Exists(args[j]))
                            itemRefList.Add(Path.GetFullPath(args[j]));
                        else
                            break;
                    }

                    itemInputs.Add(new Tuple<int, List<string>>(int.Parse(args[i + 1]), itemRefList));
                }

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
                    debug = true;

                if (args[i] == "-t" && i + 1 < args.Length)
                    fightFuncTable = File.ReadAllLines(args[i + 1]);

                if (args[i] == "-v")
                    quiet = false;

                if (args[i] == "-b" && i + 1 < args.Length)
                    buildPath = Path.GetFullPath(args[i + 1]);
                
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

            // create file
            HSDRawFile newfile = null;
            HSDRawFile injectfile = null;
            SBM_FighterData ftData = null;

            // create new file
            if (!string.IsNullOrEmpty(output))
                newfile = new HSDRawFile();

            // inject existing dat file (or create new one if not found)
            if (!string.IsNullOrEmpty(datFile))
                injectfile = File.Exists(datFile) ? new HSDRawFile(datFile) : new HSDRawFile();

            // find fighter data if it exists
            if (injectfile != null && injectfile.Roots.Count > 0 && injectfile.Roots[0].Data is SBM_FighterData ftdata)
                ftData = ftdata;

            // static param table
            var param_table = new string[] { "param_ext" };

            // single table compile
            if (itemInputs.Count == 0)
            {
                var elfs = CompileElfs(inputs.ToArray(), disableWarnings, clean, opLevel, includes.ToArray(), buildPath, debug, quiet);

                //foreach (var f in Directory.GetFiles(@"C:\devkitPro\libogc\lib\cube"))
                //foreach (var f in Directory.GetFiles(@"C:\devkitPro\devkitPPC\powerpc-eabi\lib"))
                //{
                //    if (Path.GetExtension(f).Equals(".a"))
                //        elfs.AddRange(FighterFunction.LibArchive.GetElfs(f));
                //    if (Path.GetExtension(f).Equals(".o"))
                //        elfs.Add(new RelocELF(File.ReadAllBytes(f)));
                //}
                //elfs.AddRange(FighterFunction.LibArchive.GetElfs(@"C:\devkitPro\devkitPPC\lib\gcc\powerpc-eabi\10.2.0\libgcc.a"));
                //elfs.AddRange(FighterFunction.LibArchive.GetElfs(@"C:\Users\ploaj\Desktop\Modlee\libgc\MemCardDemo\libogc.a"));
                var lelf = GenerateLinkedElf(elfs, fightFuncTable, linkFile, quiet);
                
                // check for special attribute symbol
                if (ftData != null)
                {
                    foreach (var elf in elfs)
                    {
                        if (elf.SymbolEnumerator.Any(e => e.Symbol.Equals("param_ext")))
                        {
                            var special_attr = CmdGenerateDatFile.BuildDatFile(elf, param_table);

                            if (special_attr != null && special_attr.Roots.Count > 0 && special_attr["param_ext"] != null)
                            {
                                // Console.WriteLine("Found Param_Ext... adding to fighter data...");
                                ftData.Attributes2 = special_attr["param_ext"].Data;
                                break;
                            }
                        }
                    }
                }

                function = GenerateFunctionData(lelf, linkFile, fightFuncTable, quiet, debug);
            }
            else
            {
                // item table compile
                function = new HSDAccessor() { _s = new HSDStruct(4) };

                int count = 0;
                foreach (var f in itemInputs)
                {
                    count = Math.Max(count, f.Item1 + 1);

                    if (4 + 4 * (f.Item1 + 1) > function._s.Length)
                        function._s.Resize(4 + 4 * (f.Item1 + 1));

                    var elfs = CompileElfs(f.Item2.ToArray(), disableWarnings, clean, opLevel, includes.ToArray(), buildPath, debug, quiet);
                    var lelf = GenerateLinkedElf(elfs, fightFuncTable, linkFile, quiet);

                    // check for special attribute symbol
                    if (ftData != null)
                    {
                        foreach (var elf in elfs)
                        {
                            if (elf.SymbolEnumerator.Any(e => e.Symbol.Equals("param_ext")))
                            {
                                var special_attr = CmdGenerateDatFile.BuildDatFile(elf, param_table);

                                if (special_attr != null && special_attr.Roots.Count > 0 && special_attr["param_ext"] != null)
                                {
                                    // Console.WriteLine("Found Param_Ext... adding to fighter data...");
                                    ftData.Articles.Articles[f.Item1].ParametersExt = special_attr["param_ext"].Data;
                                    break;
                                }
                            }
                        }
                    }

                    var relocFunc = GenerateFunctionData(lelf, linkFile, fightFuncTable, quiet, debug);
                    function._s.SetReference(4 + 0x04 * f.Item1, relocFunc);
                }
                function._s.SetInt32(0x00, count);
            }

            // inject symbol to dat file and save
            if (function != null)
            {
                if (newfile != null)
                {
                    DatTools.InjectSymbolIntoDat(newfile, symbolName, function);
                    newfile.Save(output);
                    Console.WriteLine("saving " + output + "...");
                }

                if (injectfile != null)
                {
                    DatTools.InjectSymbolIntoDat(injectfile, symbolName, function);
                    injectfile.Save(datFile);
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
        private static List<RelocELF> CompileElfs(string[] inputs, bool disableWarnings, bool clean, int optimizationLevel = 2, string[] includes = null, string buildPath = null, bool debug = false, bool quiet = true)
        {
            return Compiling.Compile(inputs, disableWarnings, clean, optimizationLevel, includes, buildPath, debug, quiet);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="fightFuncTable"></param>
        /// <param name="quiet"></param>
        /// <returns></returns>
        private static LinkedELF GenerateLinkedElf(List<RelocELF> elfFiles, string[] funcTable, LinkFile link, bool quiet)
        {
            return LinkELFs(elfFiles, link, funcTable, quiet);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="fightFuncTable"></param>
        /// <param name="quiet"></param>
        /// <returns></returns>
        private static HSDAccessor GenerateFunctionData(LinkedELF lelf, LinkFile link, string[] funcTable, bool quiet, bool debug)
        {
            return GenerateFunctionDAT(lelf, link, funcTable, debug, quiet);
        }


    }
}
