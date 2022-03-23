using HSDRaw;
using HSDRaw.Common;
using HSDRaw.Common.Animation;
using HSDRaw.Melee.Pl;
using HSDRaw.Tools;
using HSDRaw.Tools.Melee;
using System.IO;
using System.Linq;

namespace MexTK.Commands.Animation
{
    public class CmdFighterAnimInject : ICommand
    {
        public bool DoIt(string[] args)
        {
            if (args.Length >= 3)
            {
                // parse args
                var plxxfile = Path.GetFullPath(args[1]);
                var plxxajfile = Path.GetFullPath(args[2]);
                var plxxnrfile = Path.GetFullPath(args[3]);

                var anim_pathfile = Path.GetFullPath(args[4]);
                var symbol = args[5];

                HSD_FigaTree anim_to_inject = null;

                // rendering files not loaded
                if (string.IsNullOrEmpty(plxxfile) || string.IsNullOrEmpty(plxxajfile) || string.IsNullOrEmpty(anim_pathfile))
                    return false;


                // check if anim is maya anim
                if (Path.GetExtension(anim_pathfile).ToLower().Contains("anim"))
                {
                    // todo: joint map
                    anim_to_inject = Tools.ConvMayaAnim.ImportFromMayaAnim(anim_pathfile, null);

                    // optimize
                    var nodes = anim_to_inject.Nodes;

                    var joints = (new HSDRawFile(plxxnrfile).Roots[0].Data as HSD_JOBJ).BreathFirstList;
                    int nodeIndex = 0;
                    foreach (var n in nodes)
                    {
                        var joint = joints[nodeIndex];

                        var tracks = n.Tracks.Select(e => new FOBJ_Player(e.ToFOBJ())).ToList();

                        AnimationKeyCompressor.OptimizeJointTracks(joint, ref tracks);

                        n.Tracks = tracks.Select(e => {
                            var tr = new HSD_Track();
                            tr.FromFOBJ(e.ToFobj());
                            return tr;
                            }).ToList();

                        nodeIndex++;
                    }

                    anim_to_inject.Nodes = nodes;
                }
                else
                // check if anim if figatree
                try
                {
                    HSDRawFile f = new HSDRawFile(anim_pathfile);
                    if (f.Roots.Count < 1)
                        return false;

                    if (f.Roots[0].Data is HSD_FigaTree ani)
                        anim_to_inject = ani;
                    else
                        return false;
                }
                catch
                {
                    return false;
                }

                // make sure anim isn't null
                if (anim_to_inject == null)
                    return false;

                try
                {
                    var plxx = new HSDRawFile(plxxfile);

                    FighterAJManager animManager = new FighterAJManager();
                    animManager.ScanAJFile(plxxajfile);

                    var newAnim = new HSDRawFile();
                    newAnim.Roots.Add(new HSDRootNode() { Name = symbol, Data = anim_to_inject });

                    using (MemoryStream stream = new MemoryStream())
                    {
                        // set new animation
                        newAnim.Save(stream);
                        animManager.SetAnimation(symbol, stream.ToArray());

                        var ftdata = plxx.Roots[0].Data as SBM_FighterData;
                        var commands = ftdata.FighterActionTable.Commands;

                        var symbols = commands.Select(e => e.SymbolName.Value).ToArray();

                        var rebuild = animManager.RebuildAJFile(symbols, true);

                        foreach (var c in commands)
                        {
                            if (c.SymbolName != null && !string.IsNullOrEmpty(c.SymbolName.Value))
                            {
                                var sizeoffset = animManager.GetOffsetSize(c.SymbolName.Value);
                                c.AnimationOffset = sizeoffset.Item1;
                                c.AnimationSize = sizeoffset.Item2;
                            }
                        }

                        // save files
                        plxx.Save(plxxfile);
                        File.WriteAllBytes(plxxajfile, rebuild);
                    }

                } catch
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public string Help()
        {
            return @"MexTK.exe -ifa (input PlXx.dat) (input PlXxAJ.dat) (PlXxNr.dat) (Animation File (figatree or maya anim)) (animation symbol)";
        }

        public string ID()
        {
            return "-ifa";
        }

        public string Name()
        {
            return "Fighter Action Anim Inject";
        }
    }
}
