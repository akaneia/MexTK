using HSDRaw;
using HSDRaw.Common.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MexTK.Commands
{
    public class CmdOptimizeFigatree : ICommand
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "Optimize Figatree";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Help()
        {
            return @"MexTK.exe -oft (input dat) (output dat)";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ID()
        {
            return "-oft";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool DoIt(string[] args)
        {
            if (args.Length >= 3)
            {
                // parse args
                var targetAnimFile = Path.GetFullPath(args[1]);
                var destAnimFile = Path.GetFullPath(args[2]);

                // retarget animation
                HSDRawFile inputAnim = new HSDRawFile(targetAnimFile);

                var targetTree = inputAnim.Roots[0].Data as HSD_FigaTree;

                List<FigaTreeNode> newNodes = targetTree.Nodes;

                var keydecrease = 0;
                
                foreach (var n in newNodes)
                {
                    foreach(var t in n.Tracks)
                    {
                        var fobj = t.ToFOBJ();
                        var keys = fobj.GetDecodedKeys();

                        if(keys.Count > targetTree.FrameCount * 0.8f)
                        {
                            var before = keys.Count;
                            keys = Tools.LineSimplification.Simplify(keys, 0.025f);
                            fobj.SetKeys(keys, fobj.JointTrackType);
                            t.FromFOBJ(fobj);

                            keydecrease += before - keys.Count;
                        }
                    }
                }

                Console.WriteLine("Optimized " + keydecrease + " removed");

                targetTree.Nodes = newNodes;

                inputAnim.Save(destAnimFile);

                return true;
            }

            return false;
        }
    }
}
