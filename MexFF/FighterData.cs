using HSDRaw;
using HSDRaw.Melee.Pl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MexFF
{
    public class FighterData
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fighterDirectory"></param>
        /// <param name="animDirectory"></param>
        /// <param name="rstDirectory"></param>
        public static Tuple<HSDRawFile, byte[], HSDRawFile> BuildFighterData(string symbol, string rstSymbol, string fighterDirectory, string animDirectory, string rstDirectory)
        {
            SBM_FighterData data = new SBM_FighterData();

            byte[] ajFile = null;
            HSDRawFile rstFile = null;

            foreach(var file in Directory.GetFiles(fighterDirectory))
            {
                var fname = Path.GetFileName(file);

                if (fname.StartsWith("unknown"))
                {
                    fname = fname.Replace("unknown", "");
                    fname = fname.Replace(".dat", "");
                    var off = int.Parse(fname, System.Globalization.NumberStyles.HexNumber);
                    data._s.SetReference(off, ImportDAT(file));
                    continue;
                }
                
                switch (fname)
                {
                    case "attributes.ini":
                        data.Attributes = new SBM_CommonFighterAttributes() { _s = ImportINI(file)._s };
                        break;
                    case "attributes_unique.ini":
                        data.Attributes2 = ImportINI(file);
                        break;
                    case "attributes_unique.dat":
                        data.Attributes2 = ImportDAT(file);
                        break;
                    case "bone_table.ini":
                        data.FighterBoneTable = new SBM_FighterBoneIDs() { _s = ImportINI(file)._s };
                        break;
                    case "ledge_grabbox.ini":
                        data.LedgeGrabBox = new SBM_LedgeGrabBox() { _s = ImportINI(file)._s };
                        break;
                    case "model_animation_parts.dat":
                        data.ModelPartAnimations = new SBM_ModelPartTable() { _s = ImportDAT(file)._s };
                        break;
                    case "shield_pose.dat":
                        data.ShieldPoseContainer = new SBM_ShieldModelContainer() { _s = ImportDAT(file)._s };
                        break;
                    case "physics.dat":
                        data.Physics = new SBM_PhysicsGroup() { _s = ImportDAT(file)._s };
                        break;
                    case "hurtboxes.dat":
                        data.Hurtboxes = new SBM_HurtboxBank<SBM_Hurtbox>() { _s = ImportDAT(file)._s };
                        break;
                    case "articles.dat":
                        data.Articles = new SBM_ArticlePointer() { _s = ImportDAT(file)._s };
                        break;
                    case "model_lookup_table.dat":
                        data.ModelLookupTables = new SBM_PlayerModelLookupTables() { _s = ImportDAT(file)._s };
                        break;
                    case "common_sound_table.dat":
                        data.CommonSoundEffectTable = new SBM_PlayerSFXTable() { _s = ImportDAT(file)._s };
                        break;
                    case "reflect_model_joint.dat":
                        data.ShadowModel = new HSDRaw.Common.HSD_JOBJ() { _s = ImportDAT(file)._s };
                        break;
                    case "subactions_dynamic.dat":
                        data.SubActionDynamicBehaviors = new SBM_DynamicBehaviorIDs() { _s = ImportDAT(file)._s };
                        break;
                    case "subactions_result_dynamic.dat":
                        data.WinSubActionDynamicBehaviors = new SBM_DynamicBehaviorIDs() { _s = ImportDAT(file)._s };
                        break;
                    case "subactions.dat":
                        {
                            var sat = new SBM_SubActionTable() { _s = ImportDAT(file)._s };
                            var sa = sat.Subactions;
                            ajFile = ImportAnimations(animDirectory, sa);
                            sat.Subactions = sa;
                            data.SubActionTable = sat;
                        }
                        break;
                    case "subactions_result.dat":
                        {
                            rstFile = new HSDRawFile();

                            var sat = new SBM_SubActionTable() { _s = ImportDAT(file)._s };
                            var sa = sat.Subactions;
                            rstFile.Roots.Add(new HSDRootNode() { Name = rstSymbol, Data = new HSDAccessor() { _s = new HSDStruct(ImportAnimations(rstDirectory, sa)) } });
                            sat.Subactions = sa;

                            data.WinSubAction = sat;
                        }
                        break;
                }
            }

            HSDRawFile f = new HSDRawFile();
            f.Roots.Add(new HSDRootNode() { Name = symbol, Data = data });
            return new Tuple<HSDRawFile, byte[], HSDRawFile>(f, ajFile, rstFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fighterDataPath"></param>
        /// <param name="ajPath"></param>
        public static void ExportFighterData(string fighterDataPath, string ajPath, string rstPath)
        {
            var f = new HSDRawFile(fighterDataPath);

            var directory = Path.Combine(Path.GetDirectoryName(fighterDataPath), Path.GetFileNameWithoutExtension(fighterDataPath) + "\\");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var directoryAnim = string.IsNullOrEmpty(ajPath) ? "" : Path.Combine(Path.GetDirectoryName(ajPath), Path.GetFileNameWithoutExtension(ajPath) + "\\");
            if (!string.IsNullOrEmpty(ajPath) && !Directory.Exists(directoryAnim))
                Directory.CreateDirectory(directoryAnim);

            var directoryRst = string.IsNullOrEmpty(rstPath) ? "" : Path.Combine(Path.GetDirectoryName(rstPath), Path.GetFileNameWithoutExtension(rstPath) + "\\");
            if (!string.IsNullOrEmpty(rstPath) && !Directory.Exists(directoryRst))
                Directory.CreateDirectory(directoryRst);

            Console.WriteLine($"Extracting {Path.GetFileName(fighterDataPath)} to {Path.GetFileNameWithoutExtension(fighterDataPath) + "\\"}...");
            
            if(f.Roots.Count > 0 && f.Roots[0].Data is SBM_FighterData data)
            {
                // Extract Attributes
                ExportINI(Path.Combine(directory, "attributes.ini"), data.Attributes);
                ExportINI(Path.Combine(directory, "ledge_grabbox.ini"), data.LedgeGrabBox);
                ExportINI(Path.Combine(directory, "bone_table.ini"), data.FighterBoneTable);

                // Extract Various structures
                ExportDAT(Path.Combine(directory, "attributes_unique.dat"), data.Attributes2);
                ExportDAT(Path.Combine(directory, "model_animation_parts.dat"), data.ModelPartAnimations);
                ExportDAT(Path.Combine(directory, "shield_pose.dat"), data.ShieldPoseContainer);
                ExportDAT(Path.Combine(directory, "unknown24.dat"), data.Unknown0x24);
                if (data.Unknown0x28 != null)
                    ExportDAT(Path.Combine(directory, "unknown28.dat"), data.Unknown0x28);
                ExportDAT(Path.Combine(directory, "physics.dat"), data.Physics);
                ExportDAT(Path.Combine(directory, "hurtboxes.dat"), data.Hurtboxes);
                ExportDAT(Path.Combine(directory, "unknown34.dat"), data.Unknown0x34);
                ExportDAT(Path.Combine(directory, "unknown38.dat"), data.Unknown0x38);
                ExportDAT(Path.Combine(directory, "unknown3C.dat"), data.Unknown0x3C);
                ExportDAT(Path.Combine(directory, "unknown40.dat"), data.Unknown0x40);
                ExportDAT(Path.Combine(directory, "articles.dat"), data.Articles);
                ExportDAT(Path.Combine(directory, "common_sound_table.dat"), data.CommonSoundEffectTable);
                ExportDAT(Path.Combine(directory, "unknown50.dat"), data.Unknown0x50);
                ExportDAT(Path.Combine(directory, "unknown58.dat"), data.Unknown0x58);
                ExportDAT(Path.Combine(directory, "reflect_model_joint.dat"), data.ShadowModel);

                ExportDAT(Path.Combine(directory, "model_lookup_table.dat"), data.ModelLookupTables);

                // export animations

                if(!string.IsNullOrEmpty(ajPath))
                    ExportAnims(directoryAnim, File.ReadAllBytes(ajPath), data.SubActionTable.Subactions);
                if (!string.IsNullOrEmpty(rstPath))
                    ExportAnims(directoryRst, new HSDRawFile(rstPath).Roots[0].Data._s.GetData(), data.WinSubAction.Subactions);

                // Extract Subaction Table and Animations

                ExportDAT(Path.Combine(directory, "subactions.dat"), data.SubActionTable);
                ExportDAT(Path.Combine(directory, "subactions_dynamic.dat"), data.SubActionDynamicBehaviors);
                ExportDAT(Path.Combine(directory, "subactions_result.dat"), data.WinSubAction);
                ExportDAT(Path.Combine(directory, "subactions_result_dynamic.dat"), data.WinSubActionDynamicBehaviors);

            }

            Console.WriteLine($"Done");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="animData"></param>
        /// <param name="subactions"></param>
        private static void ExportAnims(string directory, byte[] animData, SBM_FighterSubAction[] subactions)
        {
            HashSet<int> exported = new HashSet<int>();
            foreach (var s in subactions)
            {
                if (!string.IsNullOrEmpty(s.Name) && !exported.Contains(s.AnimationOffset))
                {
                    if (s.Name.Contains("IntroR"))
                        continue;

                    s.Name = s.Name.Replace("Fox", "Wolf");

                    var anim = new byte[s.AnimationSize];
                    Array.Copy(animData, s.AnimationOffset, anim, 0, anim.Length);
                    
                    var af = new HSDRawFile(anim);
                    af.Roots[0].Name = af.Roots[0].Name.Replace("Fox", "Wolf");
                    af.Save(Path.Combine(directory, s.Name + ".dat"));
                    
                    exported.Add(s.AnimationOffset);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="subactions"></param>
        private static byte[] ImportAnimations(string directory, SBM_FighterSubAction[] subactions)
        {
            var usedSymbols = subactions.Select(e => e.Name).Distinct();

            List<byte> data = new List<byte>();
            Dictionary<string, Tuple<int, int>> symbolToLocation = new Dictionary<string, Tuple<int, int>>();

            foreach(var v in Directory.GetFiles(directory))
            {
                if (v.EndsWith(".dat"))
                {
                    var f = new HSDRawFile(v);
                    if(f.Roots.Count > 0 && usedSymbols.Contains(f.Roots[0].Name))
                    {
                        var d = File.ReadAllBytes(v);
                        symbolToLocation.Add(f.Roots[0].Name, new Tuple<int, int>(data.Count, d.Length));
                        data.AddRange(d);
                        if (data.Count % 0x20 != 0)
                        {
                            var pad = 0x20 - (data.Count % 0x20);
                            for (int i = 0; i < pad; i++)
                                data.Add(0xFF);
                        }
                    }
                }
            }

            foreach(var s in subactions)
            {
                if (!string.IsNullOrEmpty(s.Name) && symbolToLocation.ContainsKey(s.Name))
                {
                    var loc = symbolToLocation[s.Name];
                    s.AnimationOffset = loc.Item1;
                    s.AnimationSize = loc.Item2;
                }
            }

            return data.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="acc"></param>
        private static void ExportDAT(string filePath, HSDAccessor acc)
        {
            HSDRawFile f = new HSDRawFile();
            f.Roots.Add(new HSDRootNode() { Name = Path.GetFileNameWithoutExtension(filePath) , Data = acc});
            f.Save(filePath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="acc"></param>
        private static HSDAccessor ImportDAT(string filePath)
        {
            return new HSDRawFile(filePath).Roots[0].Data;
        }

        /// <summary>
        /// 
        /// </summary>
        public static void ExportINI(string filePath, HSDAccessor acc)
        {
            var type = acc.GetType();


            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            using (StreamWriter w = new StreamWriter(stream))
                foreach (var t in type.GetProperties())
                {
                    if (t.Name.Equals("TrimmedSize"))
                        continue;

                    w.WriteLine($"{t.PropertyType.Name} = {t.GetValue(acc).ToString()}; // {t.Name}");
                }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static HSDAccessor ImportINI(string filePath)
        {
            var input = GetInputLines(File.ReadAllText(filePath));

            HSDAccessor acc = new HSDAccessor();

            var loc = 0;
            foreach (var i in input)
            {
                if (string.IsNullOrEmpty(i))
                    continue;

                var args = i.Split('=');

                if (args.Length != 2)
                    throw new FormatException(i + " is in incorrect format");

                var typeName = args[0];
                var value = args[1];
                var typeSize = GetTypeSize(typeName);

                acc._s.Resize(loc + typeSize);
                SetType(acc, loc, typeName, value);
                loc += typeSize;
            }

            return acc;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static int GetTypeSize(string type)
        {
            switch (type)
            {
                case "Byte":
                case "SByte":
                    return 1;
                case "Int16":
                case "UInt16":
                    return 2;
                case "Int32":
                case "UInt32":
                case "Single":
                case "WeightDependentFlag":
                    return 4;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="acc"></param>
        /// <param name="loc"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static void SetType(HSDAccessor acc, int loc, string type, string value)
        {
            switch (type)
            {
                case "Byte":
                    acc._s.SetByte(loc, byte.Parse(value));
                    break;
                case "SByte":
                    acc._s.SetByte(loc, (byte)sbyte.Parse(value));
                    break;
                case "UInt16":
                    acc._s.SetInt16(loc, (short)ushort.Parse(value));
                    break;
                case "Int16":
                    acc._s.SetInt16(loc, short.Parse(value));
                    break;
                case "UInt32":
                    acc._s.SetInt32(loc, (int)uint.Parse(value));
                    break;
                case "Int32":
                    acc._s.SetInt32(loc, int.Parse(value));
                    break;
                case "Single":
                    acc._s.SetFloat(loc, float.Parse(value));
                    break;
                case "WeightDependentFlag":
                    acc._s.SetInt32(loc, (int)Enum.Parse(typeof(WeightDependentFlag), value));
                    break;
                default:
                    throw new Exception("Unsupported Type " + type);
            }
        }

        static string[] GetInputLines(string code)
        {
            code = StripComments(code);
            code = StripSpaces(code);
            return code.Split(';');
        }

        static string StripComments(string code)
        {
            var re = @"(@(?:""[^""]*"")+|""(?:[^""\n\\]+|\\.)*""|'(?:[^'\n\\]+|\\.)*')|//.*|/\*(?s:.*?)\*/";
            return Regex.Replace(code, re, "$1");
        }

        static string StripSpaces(string code)
        {
            return Regex.Replace(code, @"\s+", string.Empty); ;
        }

    }
}
