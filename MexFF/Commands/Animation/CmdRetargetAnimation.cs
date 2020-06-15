using HSDRaw;
using HSDRaw.Common;
using HSDRaw.Common.Animation;
using MexTK.Tools;
using System.IO;

namespace MexTK.Commands.Animation
{
    /// <summary>
    /// TODO: This does not work properly
    /// </summary>
    public class CmdRetargetAnimation : ICommand
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "Reorient Figatree";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Help()
        {
            return @"MexTK.exe -rtft (source joint) (source bone ini) (target joint) (target bone ini) (source figatree) (output figatree)";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ID()
        {
            return "-rtft";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool DoIt(string[] args)
        {
            if (args.Length >= 6)
            {
                // parse args
                var inputjoint = Path.GetFullPath(args[1]);
                var inputBoneMap = new BoneMap(Path.GetFullPath(args[2]));
                var targetjoint = Path.GetFullPath(args[3]);
                var targetBoneMap = new BoneMap(Path.GetFullPath(args[4]));
                var inputfigatree = Path.GetFullPath(args[5]);
                var outputfigatree = Path.GetFullPath(args[6]);

                // source joint
                HSDRawFile inputJoint = new HSDRawFile(inputjoint);
                var sourceJOBJ = inputJoint.Roots[0].Data as HSD_JOBJ;

                // target joint
                HSDRawFile targetJoint = new HSDRawFile(targetjoint);
                var targetJOBJ = targetJoint.Roots[0].Data as HSD_JOBJ;

                // retarget animation
                HSDRawFile inputAnim = new HSDRawFile(inputfigatree);
                var tree = inputAnim.Roots[0].Data as HSD_FigaTree;
                
                inputAnim.Roots[0].Data = AnimationBakery.Port(tree, sourceJOBJ, targetJOBJ, inputBoneMap, targetBoneMap);
                inputAnim.Save(outputfigatree);

                return true;
            }

            return false;
        }
    }
}
