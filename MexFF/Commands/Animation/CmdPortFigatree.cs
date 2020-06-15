using HSDRaw;
using HSDRaw.Common;
using HSDRaw.Common.Animation;
using HSDRaw.Tools;
using MexTK.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

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
                var target = new BoneMap(boneIniTarget);
                var dest = new BoneMap(boneInitDest);
                
                SkeletonPorter port = null;
                List<HSD_JOBJ> jobjsFrom = null;
                List<HSD_JOBJ> jobjsTo = null;

                if (args.Length >= 8)
                {
                    jobjsFrom = (new HSDRawFile(args[6]).Roots[0].Data as HSD_JOBJ).BreathFirstList;
                    jobjsTo = (new HSDRawFile(args[7]).Roots[0].Data as HSD_JOBJ).BreathFirstList;
                    port = new SkeletonPorter(
                        new HSDRawFile(args[6]).Roots[0].Data as HSD_JOBJ,
                        new HSDRawFile(args[7]).Roots[0].Data as HSD_JOBJ,
                        target,
                        dest);
                }
                
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

                    boneName = target.GetName(nodeIndex);
                    var ind = dest.GetIndex(boneName);

                    if (ind != -1)
                    {
                        //System.Diagnostics.Debug.WriteLine($"{boneName} {nodeIndex} {ind}");

                        if (port != null && port.HasBone(boneName))
                        {
                            var jobj = jobjsFrom[nodeIndex];

                            FOBJ_Player rx = new FOBJ_Player(); rx.Keys.Add(new FOBJKey() { Value = jobj.RX, InterpolationType = GXInterpolationType.HSD_A_OP_CON });
                            FOBJ_Player ry = new FOBJ_Player(); ry.Keys.Add(new FOBJKey() { Value = jobj.RY, InterpolationType = GXInterpolationType.HSD_A_OP_CON });
                            FOBJ_Player rz = new FOBJ_Player(); rz.Keys.Add(new FOBJKey() { Value = jobj.RZ, InterpolationType = GXInterpolationType.HSD_A_OP_CON });
                            
                            HSD_Track rotXTrack = new HSD_Track();
                            HSD_Track rotYTrack = new HSD_Track();
                            HSD_Track rotZTrack = new HSD_Track();
                            
                            foreach (var track in n.Tracks)
                            {
                                var fobj = track.FOBJ;
                                var keys = fobj.GetDecodedKeys();
                                switch (fobj.JointTrackType)
                                {
                                    case JointTrackType.HSD_A_J_ROTX:
                                        rx.Keys = keys;
                                        break;
                                    case JointTrackType.HSD_A_J_ROTY:
                                        ry.Keys = keys;
                                        break;
                                    case JointTrackType.HSD_A_J_ROTZ:
                                        rz.Keys = keys;
                                        break;
                                    default:
                                        //newNodes[ind].Tracks.Add(track);
                                        break;
                                }
                            }

                            rotXTrack.FOBJ = OrientTrack(boneName, port, rx.Keys, JointTrackType.HSD_A_J_ROTX, jobj, jobjsTo[nodeIndex]);
                            rotYTrack.FOBJ = OrientTrack(boneName, port, ry.Keys, JointTrackType.HSD_A_J_ROTY, jobj, jobjsTo[nodeIndex]);
                            rotZTrack.FOBJ = OrientTrack(boneName, port, rz.Keys, JointTrackType.HSD_A_J_ROTZ, jobj, jobjsTo[nodeIndex]);

                            newNodes[ind].Tracks.Add(rotXTrack);
                            newNodes[ind].Tracks.Add(rotYTrack);
                            newNodes[ind].Tracks.Add(rotZTrack);

                            foreach(var track in newNodes[ind].Tracks)
                            {
                                //System.Diagnostics.Debug.WriteLine(track.FOBJ.JointTrackType + " " + track.FOBJ.GetDecodedKeys().Count);
                            }
                        }
                        else
                        if (ind < newNodes.Count && ind >= 0)
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
        /// <param name="track"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        private static HSD_FOBJ OrientTrack(string boneName, SkeletonPorter port, List<FOBJKey> keys, JointTrackType trackType, HSD_JOBJ sourceBone, HSD_JOBJ targetBone)
        {
            //var frames = keys.Select(e => e.Frame);//.Join(y.Keys.Select(e=>e.Frame).Join(z.Keys.Select(e=>e.Frame))).Distinct();
            
            Vector3 unit = Vector3.Zero;

            switch (trackType)
            {
                case JointTrackType.HSD_A_J_ROTX:
                    unit = Vector3.UnitX;
                    break;
                case JointTrackType.HSD_A_J_ROTY:
                    unit = Vector3.UnitY;
                    break;
                case JointTrackType.HSD_A_J_ROTZ:
                    unit = Vector3.UnitZ;
                    break;
            }

            var newTrack = port.ReOrientParent(unit, boneName);

            if (Math.Abs(Math.Abs(newTrack.X) - 1) < 0.001f)
                trackType = JointTrackType.HSD_A_J_ROTX;

            if (Math.Abs(Math.Abs(newTrack.Y) - 1) < 0.001f)
                trackType = JointTrackType.HSD_A_J_ROTY;

            if (Math.Abs(Math.Abs(newTrack.Z) - 1) < 0.001f)
                trackType = JointTrackType.HSD_A_J_ROTZ;


            /*var k = keys[0];
            keys.Clear();
            k.InterpolationType = GXInterpolationType.HSD_A_OP_KEY;
            keys.Add(k);*/

            int index = 0;
            foreach (var f in keys)
            {
                var rot = unit * f.Value;
                rot -= new Vector3(sourceBone.RX, sourceBone.RY, sourceBone.RZ);
                rot = port.ReOrientParent(rot, boneName);
                rot += new Vector3(targetBone.RX, targetBone.RY, targetBone.RZ);

                if (boneName.Equals("LLegJA"))
                    System.Diagnostics.Debug.WriteLine($"{boneName} {trackType} {rot} {f.Value}");
                if (boneName.Equals("RShoulderN"))
                    System.Diagnostics.Debug.WriteLine($"{boneName} {trackType} {rot} {f.Value}");
                if (boneName.Equals("LKneeJ"))
                    System.Diagnostics.Debug.WriteLine($"{boneName} {trackType} {rot} {f.Value}");

                switch (trackType)
                {
                    case JointTrackType.HSD_A_J_ROTX:
                        f.Value = rot.X;
                        break;
                    case JointTrackType.HSD_A_J_ROTY:
                        f.Value = rot.Y;
                        break;
                    case JointTrackType.HSD_A_J_ROTZ:
                        f.Value = rot.Z;
                        break;
                }
                index++;
            }

            var fobj = new HSD_FOBJ();
            fobj.SetKeys(keys, trackType);
            return fobj;
        }
    }
}
