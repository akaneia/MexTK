using System;
using System.IO;

namespace MexFF
{
    class Program
    {
        static void Main(string[] args)
        {
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

            if (string.IsNullOrEmpty(input) || args.Length == 0)
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

            if (!File.Exists(input) || (Path.GetExtension(input) != ".c" && Path.GetExtension(input) != ".o"))
            {
                Console.WriteLine($"Error:\n{input} does not exist or is invalid\nPress enter to exit");
                Console.ReadLine();
                return;
            }

            if (string.IsNullOrEmpty(output))
                output = Path.Combine(Path.GetDirectoryName(input), Path.GetFileName(input).Replace(Path.GetExtension(input), ".dat"));
            
            if (File.Exists(output))
            {
                Console.WriteLine(output + " already exists, overwrite? (y/n)");
                if (Console.ReadLine().Trim().ToLower() != "y")
                    return;
            }

            var ext = Path.GetExtension(input).ToLower();

            if (ext.Equals(".o"))
            {
                try
                {
                    ELFTools.ELFToDAT(input, output, symbolName, fightFuncTable, injectDat, quiet);

                    Console.WriteLine();
                    Console.WriteLine("Sucessfully Converted ELF to DAT!");
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
                    ELFTools.CToDAT(input, output, symbolName, fightFuncTable, injectDat, quiet);

                    Console.WriteLine();
                    Console.WriteLine("Sucessfully Compiled and Converted C to DAT!");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            Console.WriteLine();
            Console.WriteLine("press enter to exit...");
            Console.ReadLine();
        }

        private static void PrintInstruction()
        {
            Console.WriteLine(@"MexFF Compiler

Usage: mexff.exe (options)
Options:
    -i (file(.c, .o(ELF))) : Input File (.c and .o (elf)) files are supported
    -o (file.dat) : output dat file name
    -d (file.dat) : dat file to inject symbol into (will not output a 'new' dat file)
    -s (name) : symbol name (default is ftFunction)
    -t (file.txt) : specify symbol table list
    -q : Quiet Mode (Console doesn't print information)

Ex: mexff.exe -q -i 'main.c' -o 'ftFunction.dat'

Note: in order to compile .c files you must 
have DEVKITPPC installed with GameCube rules
as well as have the environment path setup");
        }
    }
}
