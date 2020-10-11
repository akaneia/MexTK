using System.Collections.Generic;
using System.IO;

namespace MexTK.FighterFunction
{
    public class LinkFile
    {
        private Dictionary<string, uint> List = new Dictionary<string, uint>();

        /// <summary>
        /// 
        /// </summary>
        public LinkFile()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filepath"></param>
        public LinkFile(string filepath)
        {
            LoadLinkFile(filepath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public bool ContainsSymbol(string symbol)
        {
            return List.ContainsKey(symbol);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool TryGetSymbolAddress(string symbol, out uint address)
        {
            return List.TryGetValue(symbol, out address);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filepath"></param>
        public void LoadLinkFile(string filepath)
        {
            using (FileStream stream = new FileStream(filepath, FileMode.Open))
            using (StreamReader r = new StreamReader(stream))
            {
                while(!r.EndOfStream)
                {
                    var args = SanitizeInput(r.ReadLine()).Split(':');

                    if (args.Length >= 2 &&
                        !List.ContainsKey(args[1]) &&
                        uint.TryParse(args[0], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint pos))
                        List.Add(args[1], pos);
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
