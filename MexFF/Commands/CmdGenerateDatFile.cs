using HSDRaw;
using MexTK.FighterFunction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static MexTK.FighterFunction.RelocELF;

namespace MexTK.Commands
{
    public class CmdGenerateDatFile : ICommand
    {
        public bool DoIt(string[] args)
        {
            // Parse Args
            List<string> inputs = new List<string>();
            LinkFile linkFile = new LinkFile();
            List<string> symbols = new List<string>();
            string output = "";
            bool quiet = true;
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

                if (args[i] == "-o" && i + 1 < args.Length)
                    output = Path.GetFullPath(args[i + 1]);

                if (args[i] == "-s" && i + 1 < args.Length)
                {
                    for (int j = i + 1; j < args.Length; j++)
                        if (!string.IsNullOrEmpty(args[j]))
                            symbols.Add(args[j]);
                        else
                            break;
                }

                if (args[i] == "-v")
                    quiet = false;
            }

            //
            if (symbols.Count <= 0)
            {
                Console.WriteLine("Error: No symbols specified");
                return false;
            }

            // if output is null set the name to the symbol name
            if (string.IsNullOrEmpty(output))
            {
                Console.WriteLine("Error: No output path specified");
                return false;
            }

            // load link file in mex directory
            foreach (var f in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory))
            {
                if (Path.GetExtension(f).ToLower().Equals(".link"))
                    linkFile.LoadLinkFile(f);
            }

            // print instruction and exit if no input
            if ((inputs.Count == 0)
                || args.Length == 0)
            {
                return false;
            }

            // create output path if one isn't entered
            if (string.IsNullOrEmpty(output))
                output = Path.Combine(Path.GetDirectoryName(inputs[0]), Path.GetFileName(inputs[0]).Replace(Path.GetExtension(inputs[0]), ".dat"));

            // compile code
            var elfs = Compiling.Compile(inputs.ToArray(), disableWarnings, clean, opLevel);

            // link elfs
            var lelf = new LinkedELF(elfs, linkFile, symbols.ToArray(), quiet);

            // generate file
            var file = lelf.BuildDatFile();

            // check if file was generated
            if (file == null)
                return false;

            file.Save(output);

            // create DAT file
            /*if (function != null)
            {
                // save new file
                if (!string.IsNullOrEmpty(output))
                {
                    var f = new HSDRawFile();
                    //DatTools.InjectSymbolIntoDat(f, symbolName, function);
                    f.Save(output);
                    Console.WriteLine("saving " + output + "...");
                }

                // We did it boys
                Console.WriteLine();
                Console.WriteLine("Sucessfully Compiled and Converted to DAT!");

                return true;
            }*/

            return true;
        }


        public string Help()
        {
            return "";
        }

        public string ID()
        {
            return "-dat";
        }

        public string Name()
        {
            return "Generate DAT File";
        }
    }
}
