using HSDRaw;
using System;
using System.Collections.Generic;

namespace MexTK.Commands
{
    /// <summary>
    /// add symbol from one dat file to another
    /// </summary>
    public class CmdAddSymbol : ICommand
    {
        public bool DoIt(string[] args)
        {
            if(args.Length >= 3)
            {
                var dest = args[1];
                var src = args[2];

                bool all = false;
                List<string> names = new List<string>();

                for(int i = 3; i < args.Length; i++)
                {
                    if (args[i] == "-a")
                        all = true;
                    if (args[i] == "-s" && i + 1 < args.Length)
                        names.Add(args[i + 1]);
                }

                HSDRawFile d = new HSDRawFile(dest);
                HSDRawFile s = new HSDRawFile(src);

                if(s.Roots.Count > 0)
                {
                    if (all)
                    {
                        d.Roots.AddRange(s.Roots);
                    }
                    if (names.Count > 0)
                    {
                        foreach(var n in names)
                        {
                            var sym = s.Roots.Find(e=>e.Name.Equals(n));
                            if (sym != null)
                                d.Roots.Add(sym);
                            else
                                Console.WriteLine("Could not find symbol " + n);
                        }
                    }
                    else
                    {
                        d.Roots.Add(s.Roots[0]);
                    }
                }

                d.Save(dest);
                return true;
            }

            return false;
        }

        public string Help()
        {
            return @"MexTK.exe -addSymbol (destination dat file) (source dat file) (options(see below))

add symbol(s) from one dat file to another

Options:
-a          : all symbols
-s (symbol) : symbol with name";
        }

        public string ID()
        {
            return "-addSymbol";
        }

        public string Name()
        {
            return "Add Symbol To DAT file";
        }
    }
}
