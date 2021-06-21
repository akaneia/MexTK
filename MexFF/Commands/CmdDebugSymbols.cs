using HSDRaw;
using MexTK.FighterFunction;

namespace MexTK.Commands
{
    public class CmdDebugSymbols : ICommand
    {
        public bool DoIt(string[] args)
        {
            if (args.Length >= 2)
            {
                var mapFile = args[1];

                var linkFile = (args.Length >= 3) ? args[2] : null;

                var map = new MapFile(mapFile);
                LinkFile link = null;
                if (linkFile != null)
                    link = new LinkFile(linkFile);

                var debug_symbol_table = new HSDStruct((map.Entries.Count + 1) * 0xC);
                int symbol_index = 0;
                foreach (var e in map.Entries)
                {
                    debug_symbol_table.SetInt32(symbol_index * 0xC, (int)e.Start);
                    debug_symbol_table.SetInt32(symbol_index * 0xC + 4, (int)e.End);

                    if (link != null && link.TryGetAddressSymbol(e.Start, out string sym))
                        debug_symbol_table.SetString(symbol_index * 0xC + 8, sym, true);
                    else
                        if (!e.Symbol.StartsWith("zz_"))
                        debug_symbol_table.SetString(symbol_index * 0xC + 8, e.Symbol, true);
                    symbol_index++;
                }

                var function = new HSDAccessor() { _s = new HSDStruct(0x10) };
                function._s.SetInt32(0, map.Entries.Count);
                function._s.SetReferenceStruct(0x04, debug_symbol_table);

                HSDRawFile f = new HSDRawFile();
                f.Roots.Add(new HSDRootNode() { 
                Data = function,
                Name = "mexDebug"
                });

                f.Save("MxDb.dat");


                return true;
            }

            return false;
        }

        public string Help()
        {
            return @"MexTK.exe -db (map file) (link file)";
        }

        public string ID()
        {
            return "-db";
        }

        public string Name()
        {
            return "Generate Debug Symbol DAT File";
        }
    }
}
