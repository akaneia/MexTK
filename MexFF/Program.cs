using HSDRaw;
using System;
using System.Collections.Generic;
using System.IO;

namespace MexFF
{
    class Program
    {
        static void Main(string[] args)
        {
            List<Tuple<int, string>> itemInputs = new List<Tuple<int, string>>();
            string input = "";
            string output = "";
            string symbolName = "ftFunction";
            string datFile = null;
            string[] fightFuncTable = null;
            bool injectDat = false;
            bool quiet = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-i" && i + 1 < args.Length)
                    input = args[i + 1];
                if (args[i] == "-ii" && i + 2 < args.Length)
                    itemInputs.Add(new Tuple<int, string>(int.Parse(args[i + 1]), args[i + 2]));
                if (args[i] == "-o" && i + 1 < args.Length)
                    output = args[i + 1];
                if (args[i] == "-s" && i + 1 < args.Length)
                    symbolName = args[i + 1];
                if (args[i] == "-d" && i + 1 < args.Length)
                    datFile = args[i + 1];
                if (args[i] == "-t" && i + 1 < args.Length)
                    fightFuncTable = File.ReadAllLines(args[i + 1]);
                if (args[i] == "-q")
                    quiet = true;
            }

            if(!string.IsNullOrEmpty(input) && itemInputs.Count > 0)
            {
                Console.WriteLine("Only -i or -ii can be used at once, not both");
                Console.ReadLine();
                return;
            }
            
            if (string.IsNullOrEmpty(input)
                 && itemInputs.Count == 0
                || args.Length == 0)
            {
                PrintInstruction();
                Console.ReadLine();
                return;
            }

            if (!string.IsNullOrEmpty(datFile))
            {
                output = datFile;
                injectDat = true;
            }

            if (string.IsNullOrEmpty(output))
                output = Path.Combine(Path.GetDirectoryName(input), Path.GetFileName(input).Replace(Path.GetExtension(input), ".dat"));
            
            if (File.Exists(output))
            {
                Console.WriteLine(output + " already exists, overwrite? (y/n)");
                if (Console.ReadLine().Trim().ToLower() != "y")
                    return;
            }

            HSDAccessor function = null;

            if (itemInputs.Count == 0)
            {
                //if (!CheckFileExists(input))
                //    return;
                function = CompileInput(input, fightFuncTable, quiet);
            }
            else
            {
                function = new HSDAccessor() { _s = new HSDStruct(4) };

                int count = 0;
                foreach(var f in itemInputs)
                {
                    //if (!CheckFileExists(f.Item2))
                    //    return;

                    count = Math.Max(count, f.Item1 + 1);

                    if (4 + 4 * (f.Item1 + 1) > function._s.Length)
                        function._s.Resize(4 + 4 * (f.Item1 + 1));

                    var relocFunc = CompileInput(f.Item2, fightFuncTable, quiet);
                    function._s.SetReference(4 + 0x04 * f.Item1, relocFunc);
                }
                function._s.SetInt32(0x00, count);
            }
            
            if (function != null)
            {
                HSDRawFile f;

                if (injectDat)
                    f = new HSDRawFile(output);
                else
                    f = new HSDRawFile();

                // generate root
                var root = new HSDRootNode();
                root.Name = symbolName;
                root.Data = function;

                // if this symbol already exists in file, replace it
                foreach (var ro in f.Roots)
                {
                    if (ro.Name.Equals(root.Name))
                    {
                        ro.Data = root.Data;
                    }
                }
                // if symbol is not in file, then add it
                if (f.Roots.FindIndex(e => e.Name == root.Name) == -1)
                    f.Roots.Add(root);

                // save new file
                f.Save(output);

                // We did it boys
                Console.WriteLine();
                Console.WriteLine("Sucessfully Compiled and Converted to DAT!");
            }

            Console.WriteLine();
            Console.WriteLine("press enter to exit...");
            Console.ReadLine();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool CheckFileExists(string input)
        {
            if (!File.Exists(input) || (Path.GetExtension(input) != ".c" && Path.GetExtension(input) != ".o"))
            {
                Console.WriteLine($"Error:\n{input} does not exist or is invalid\nPress enter to exit");
                Console.ReadLine();
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="fightFuncTable"></param>
        /// <param name="quiet"></param>
        /// <returns></returns>
        private static HSDAccessor CompileInput(string input, string[] fightFuncTable, bool quiet)
        {
            var ext = Path.GetExtension(input).ToLower();

            Dictionary<string, uint> funcTable = new Dictionary<string, uint>();
            for (int i = 0; i < fightFuncTable.Length; i++)
                funcTable.Add(fightFuncTable[i], (uint)i);

            if (ext.Equals(".o"))
            {
                try
                {
                    return ELFTools.ELFToDAT(input, funcTable, quiet);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            if (ext.Equals(".c"))
            {
                try
                {
                    return ELFTools.CToDAT(input, funcTable, quiet);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        private static void PrintInstruction()
        {
            Console.WriteLine(@"MexFF Compiler

Usage: mexff.exe (type) (options)
Options:
    -i (file(.c, .o(ELF))) : Input File (.c and .o (elf)) files are supported can have multiple inputs for 'it' type
    -ii (item index) (file(.c, .o(ELF))) : Specific index used for building ft tables
    -o (file.dat) : output dat file name
    -d (file.dat) : dat file to inject symbol into (will not output a 'new' dat file)
    -s (name) : symbol name (default is ftFunction for ft and itFunction for it)
    -t (file.txt) : specify symbol table list
    -q : Quiet Mode (Console doesn't print information)

Ex: mexff.exe -i 'main.c' -o 'ftFunction.dat' -q
Compile 'main.c' and outputs 'ftFunction.dat' in quiet mode

Ex: mexff.exe -ii 0 'bomb.c' -ii 3 'arrow.c' -d 'PlMan.dat' -s 'itFunction'
Creates function table with bomb.c with index 0 and arrow.c with index 3
and injects it into PlMan.dat with the symbol name itFunction

Note: in order to compile .c files you must 
have DEVKITPPC installed with GameCube rules
as well as have the environment path setup");
        }
    }
}
