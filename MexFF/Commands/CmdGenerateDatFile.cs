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

            // compile functions
            var elfs = Compiling.Compile(inputs.ToArray(), disableWarnings, clean, opLevel);

            var lelf = LinkELFs(elfs, linkFile, symbols.ToArray(), quiet);

            var file = BuildDatFile(lelf);

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lelf"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        public static HSDRawFile BuildDatFile(RelocELF elf, IEnumerable<string> symbols = null)
        {
            Dictionary<SymbolData, HSDAccessor> symbolToAccessor = new Dictionary<SymbolData, HSDAccessor>();

            if (symbols == null)
                symbols = elf.SymbolEnumerator.Select(e => e.Symbol);

            // convert to accessors
            foreach (var d in elf.SymbolEnumerator)
            {
                symbolToAccessor.Add(d, new HSDAccessor() { _s = new HSDStruct(d.Data) });
            }

            // check relocations
            foreach (var d in elf.SymbolEnumerator)
            {
                foreach (var r in d.Relocations)
                {
                    if (r.Type == RelocType.R_PPC_ADDR32)
                    {
                        if (!symbolToAccessor.ContainsKey(d))
                        {
                            //Console.WriteLine($"Warning: cannot relocate symbol \"{d.Symbol}\"");
                        }
                        else
                        if (!symbolToAccessor.ContainsKey(r.Symbol))
                        {
                            //Console.WriteLine($"Warning: cannot relocate symbol \"{r.Symbol.Symbol}\"");
                        }
                        else
                        {
                            var access = symbolToAccessor[d];
                            var symbol = symbolToAccessor[r.Symbol];
                            access._s.SetReference((int)r.Offset, symbol);
                        }
                    }
                    else
                    {
                        // TODO relocation error checking
                        //Console.WriteLine($"DAT Files don't support relocation type {r.Type}");
                        //return null;
                    }
                }
            }

            // generate dat file
            HSDRawFile file = new HSDRawFile();

            foreach (var v in symbols)
            {
                var data = elf.SymbolEnumerator.First(e => e.Symbol.Equals(v));

                Console.WriteLine("Found " + v + ": " + !(data == null));

                if (data != null)
                    file.Roots.Add(new HSDRootNode()
                    {
                        Name = v,
                        Data = symbolToAccessor[data]
                    });
            }

            return file;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lelf"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        public static HSDRawFile BuildDatFile(LinkedELF lelf, IEnumerable<string> symbols = null)
        {
            Dictionary<SymbolData, HSDAccessor> symbolToAccessor = new Dictionary<SymbolData, HSDAccessor>();

            if (symbols == null)
                symbols = lelf.AllSymbols.Select(e => e.Symbol);

            // convert to accessors
            foreach (var d in lelf.AllSymbols)
            {
                symbolToAccessor.Add(d, new HSDAccessor() { _s = new HSDStruct(d.Data) });
            }

            // check relocations
            foreach (var d in lelf.AllSymbols)
            {
                foreach (var r in d.Relocations)
                {
                    if (r.Type == RelocType.R_PPC_ADDR32)
                    {
                        symbolToAccessor[d]._s.SetReference((int)r.Offset, symbolToAccessor[r.Symbol]);
                    }
                    else
                    {
                        // TODO relocation error checking
                        //Console.WriteLine($"DAT Files don't support relocation type {r.Type}");
                        //return null;
                    }
                }
            }

            // generate dat file
            HSDRawFile file = new HSDRawFile();

            foreach (var v in symbols)
            {
                var data = lelf.AllSymbols.Find(e => e.Symbol.Equals(v));

                Console.WriteLine("Found " + v + ": " + !(data == null));

                if (data != null)
                    file.Roots.Add(new HSDRootNode()
                    {
                        Name = v,
                        Data = symbolToAccessor[data]
                    });
            }

            return file;
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
