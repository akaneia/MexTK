using System;
using System.IO;

namespace MexTK.Commands
{
    public class CmdFighterDataExtract : ICommand
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "Fighter Data Extract";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ID()
        {
            return "-ftExtract";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Help()
        {
            return @"MexTK.exe -ftExtract (filename) (main folder) (anim filename) (anim folder) (result anim filename) (result anim folder)";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool DoIt(string[] args)
        {
            if (args.Length > 3)
            {
                try
                {
                    var fName = Path.GetFullPath(args[1]);
                    var anmName = Path.GetFullPath(args[2]);
                    var rstName = Path.GetFullPath(args[3]);

                    FighterData.ExportFighterData(fName, anmName, rstName);
                    return true;
                }
                catch (Exception)
                {
                }
            }

            return false;
        }
    }
}
