using HSDRaw;
using HSDRaw.Melee.Pl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MexFF
{
    public class FighterData
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fighterDataPath"></param>
        /// <param name="ajPath"></param>
        public static void ExportFighterData(string fighterDataPath, string ajPath)
        {
            var f = new HSDRawFile(fighterDataPath);
            
            if(f.Roots.Count > 0 && f.Roots[0].Data is SBM_FighterData data)
            {
                // Extract Attributes

                // Extract Subaction Table and Animations

                // Extract Various structures
                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private static void ExportINI(HSDAccessor acc)
        {
            
        }

    }
}
