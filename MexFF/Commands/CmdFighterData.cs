using System;
using System.IO;

namespace MexTK.Commands
{
    /// <summary>
    /// 
    /// </summary>
    public class CmdFighterData : ICommand
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "Fighter Data";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ID()
        {
            return "-ft";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Help()
        {
            return @"MexTK.exe -ft (filename) (symbol) (main folder) (anim filename) (anim folder) (result anim filename) (result anim symbol) (result anim folder)";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool DoIt(string[] args)
        {
            if (args.Length > 8)
            {
                try
                {
                    var fName = Path.GetFullPath(args[1]);
                    var sym = args[2];
                    var dat = Path.GetFullPath(args[3]);
                    var anmName = Path.GetFullPath(args[4]);
                    var anm = Path.GetFullPath(args[5]);
                    var rstName = Path.GetFullPath(args[6]);
                    var rstsym = args[7];
                    var rst = Path.GetFullPath(args[8]);
                    var build = FighterData.BuildFighterData(sym, rstsym, dat, anm, rst);
                    build.Item1.Save(fName);
                    File.WriteAllBytes(anmName, build.Item2);
                    build.Item3.Save(rstName);
                    return true;
                } catch (Exception)
                {
                }
            }

            return false;
        }
    }
}
