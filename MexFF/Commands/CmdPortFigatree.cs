using HSDRaw;
using HSDRaw.Common.Animation;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MexTK.Commands
{
    public class CmdPortFigatree : ICommand
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "Port Figatree";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Help()
        {
            return @"MexTK.exe -pft (input dat) (output dat) (new symbol) (input bone ini) (output bone ini)

Ports figatree to new bone set
input dat is the figatree dat
output dat is the new file to save to
new symbol is the new symbol for the ported animation

Bone INIs are text files in this format:
JOBJ_0=TopN
JOBJ_1=TransN
ect...";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ID()
        {
            return "-pft";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool DoIt(string[] args)
        {
            if(args.Length >= 6)
            {
                // parse args
                var targetAnimFile = Path.GetFullPath(args[1]);
                var destAnimFile = Path.GetFullPath(args[2]);
                var newSymbol = args[3];
                var boneIniTarget = Path.GetFullPath(args[4]);
                var boneInitDest = Path.GetFullPath(args[5]);

                // load bone inis
                var target = LoadBoneINI(boneIniTarget);
                var dest = LoadBoneINI(boneInitDest);

                // retarget animation
                HSDRawFile inputAnim = new HSDRawFile(targetAnimFile);
                var targetTree = inputAnim.Roots[0].Data as HSD_FigaTree;
                
                List<FigaTreeNode> newNodes = new List<FigaTreeNode>();
                for (int i = 0; i < dest.Count; i++)
                    newNodes.Add(new FigaTreeNode());

                int nodeIndex = 0;
                foreach(var n in targetTree.Nodes)
                {
                    string boneName = "";

                    if (target.ContainsKey(nodeIndex))
                        boneName = target[nodeIndex];

                    if (dest.ContainsValue(boneName))
                    {
                        var ind = dest.FirstOrDefault(x => x.Value == boneName).Key;
                        System.Diagnostics.Debug.WriteLine($"{boneName} {nodeIndex} {ind}");
                        if(ind < newNodes.Count && ind >= 0)
                            newNodes[ind] = n;
                    }

                    nodeIndex++;
                }

                // save new file
                HSD_FigaTree figatree = new HSD_FigaTree();
                figatree.FrameCount = targetTree.FrameCount;
                figatree.Type = targetTree.Type;
                figatree.Nodes = newNodes;
                System.Console.WriteLine(destAnimFile);
                HSDRawFile file = new HSDRawFile();
                file.Roots.Add(new HSDRootNode() { Name = newSymbol, Data = figatree });
                file.Save(destAnimFile);

                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static Dictionary<int, string> LoadBoneINI(string filePath)
        {
            Dictionary<int, string> dict = new Dictionary<int, string>();

            var lines = File.ReadAllLines(filePath);

            foreach(var l in lines)
            {
                var a = l.Split('=');
                dict.Add(int.Parse(a[0].Trim().Replace("JOBJ_", "")), a[1].Trim());
            }

            return dict;
        }
    }
}
