using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MexTK.FighterFunction
{
    public class MapFile
    {
        public class MapEntry
        {
            public string Symbol;
            public uint Start;
            public uint End;
        }

        public List<MapEntry> Entries = new List<MapEntry>();

        /// <summary>
        /// 
        /// </summary>
        public MapFile()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filepath"></param>
        public MapFile(string filepath)
        {
            LoadMapFile(filepath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        private bool TryGetSymbolEntry(string symbol, out MapEntry address)
        {
            address = null;

            foreach (var v in Entries)
                if(v.Symbol == symbol)
                {
                    address = v;
                    return true;
                }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filepath"></param>
        public void LoadMapFile(string filepath)
        {
            Entries.Clear();
            using (FileStream stream = new FileStream(filepath, FileMode.Open))
            using (StreamReader r = new StreamReader(stream))
            {
                while (!r.EndOfStream)
                {
                    var args = SanitizeInput(r.ReadLine()).Split(' ');

                    if (args.Length >= 5 && 
                        uint.TryParse(args[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint addr) && 
                        uint.TryParse(args[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint size))
                    {
                        var name = args[4];

                        var entry = new MapEntry()
                        {
                            Symbol = name,
                            Start = addr,
                            End = addr + size
                        };

                        // if this symbol already exists, adjust its ending position
                        if (TryGetSymbolEntry(name, out MapEntry e))
                        {
                            e.End = entry.End;
                        }
                        else
                            Entries.Add(entry);
                    }
                }
            }
        }

        /// <summary>
        /// Removes comments
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private string SanitizeInput(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, @"/\/\/.*|\/\*\[^\]*\*\//g", "");
        }
    }
}
