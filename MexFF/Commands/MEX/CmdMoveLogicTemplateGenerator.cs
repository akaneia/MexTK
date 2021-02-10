using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MexTK.Commands.MEX
{
    public class CmdMoveLogicTemplateGenerator : ICommand
    {
        public bool DoIt(string[] args)
        {
            string[] inputs = new string[0];
            string outputPath = "";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-i")
                {
                    if (i + 1 < args.Length && File.Exists(args[i + 1]))
                        inputs = File.ReadAllLines(args[i + 1]);
                }

                if (args[i] == "-o")
                    if (i + 1 < args.Length)
                        outputPath = args[i + 1];
            }

            if (inputs.Length == 0 || string.IsNullOrEmpty(outputPath))
                return false;

            List<string> specialN_temp = new List<string>();
            List<string> specialS_temp = new List<string>();
            List<string> specialHi_temp = new List<string>();
            List<string> specialLw_temp = new List<string>();
            List<string> special_temp = new List<string>();

            foreach(var i in inputs)
            {
                switch(GetAttackID(i))
                {
                    case "ATKKIND_NONE":
                        special_temp.Add(i);
                        break;
                    case "ATKKIND_SPECIALN":
                        specialN_temp.Add(i);
                        break;
                    case "ATKKIND_SPECIALS":
                        specialS_temp.Add(i);
                        break;
                    case "ATKKIND_SPECIALHI":
                        specialHi_temp.Add(i);
                        break;
                    case "ATKKIND_SPECIALLW":
                        specialLw_temp.Add(i);
                        break;
                }
            }

            // generate header file
            GenerateHeader(Path.Combine(outputPath, "fighter.h"), inputs);

            // generate fighter file
            GenerateFighter(Path.Combine(outputPath, "fighter.c"), inputs, special_temp);

            // generate specialn
            GenerateTemplate(Path.Combine(outputPath, "special_n.c"), specialN_temp);

            // generate special s
            GenerateTemplate(Path.Combine(outputPath, "special_s.c"), specialS_temp);

            // generate special hi
            GenerateTemplate(Path.Combine(outputPath, "special_hi.c"), specialHi_temp);

            // generate special lw
            GenerateTemplate(Path.Combine(outputPath, "special_lw.c"), specialLw_temp);

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="states"></param>
        private static void GenerateHeader(string path, string[] states)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new StreamWriter(stream))
            {
                // generate inlcude
                w.WriteLine(@"#include <mex.h>");
                w.WriteLine();

                // generate enum
                w.WriteLine(@"enum States");
                w.WriteLine(@"{");
                w.WriteLine($"\t{string.Join(",\n\t", states.Select(e=>e.ToUpper()))}");
                w.WriteLine(@"}");
                w.WriteLine();

                foreach (var s in states)
                {
                    w.WriteLine($"void {s}_Anim(GOBJ* fighter);");
                    w.WriteLine($"void {s}_IASA(GOBJ* fighter);");
                    w.WriteLine($"void {s}_Phys(GOBJ* fighter);");
                    w.WriteLine($"void {s}_Coll(GOBJ* fighter);");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="states"></param>
        private static void GenerateFighter(string path, string[] states, IEnumerable<string> templates)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new StreamWriter(stream))
            {
                // generate inlcude
                w.WriteLine(@"#include fighter.h");

                WriteTemplate(w, "SpecialN");
                WriteTemplate(w, "SpecialAirN");
                WriteTemplate(w, "SpecialS");
                WriteTemplate(w, "SpecialAirS");
                WriteTemplate(w, "SpecialHi");
                WriteTemplate(w, "SpecialAirHi");
                WriteTemplate(w, "SpecialLw");
                WriteTemplate(w, "SpecialAirLw");

                WriteTemplates(w, templates);

                // generate enum
                w.WriteLine(@"static MoveLogic move_logic[] = 
{");

                foreach(var s in states)
                {
                    w.WriteLine($"\t// {s}");

                    w.WriteLine("\t{");

                    w.WriteLine($"\t\t295,");
                    w.WriteLine($"\t\t0x00340111,");
                    w.WriteLine($"\t\t{GetAttackID(s)},");
                    w.WriteLine($"\t\t0x0,");
                    w.WriteLine($"\t\t{s}_Anim,");
                    w.WriteLine($"\t\t{s}_IASA,");
                    w.WriteLine($"\t\t{s}_Phys,");
                    w.WriteLine($"\t\t{s}_Coll,");
                    w.WriteLine($"\t\tFighter_UpdateCameraBox");

                    w.WriteLine("\t},");
                }

                w.WriteLine("}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="states"></param>
        private static void GenerateTemplate(string path, IEnumerable<string> templates)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new StreamWriter(stream))
            {
                // generate inlcude
                w.WriteLine(@"#include fighter.h");

                w.WriteLine();

                WriteTemplates(w, templates);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="w"></param>
        /// <param name="templates"></param>
        private static void WriteTemplates(StreamWriter w, IEnumerable<string> templates)
        {
            foreach (var s in templates)
            {
                // generate enum
                w.WriteLine($"// {s}");
                WriteTemplate(w, s + "_Anim");
                WriteTemplate(w, s + "_IASA");
                WriteTemplate(w, s + "_Phys");
                WriteTemplate(w, s + "_Coll");
                w.WriteLine();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="w"></param>
        /// <param name="s"></param>
        private static void WriteTemplate(StreamWriter w, string s)
        {
            w.WriteLine($"void {s}(GOBJ *fighter)");

            w.WriteLine("{");

            w.WriteLine("\tFighterData *fighter_data = fighter->userdata;");

            w.WriteLine("}");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateName"></param>
        /// <returns></returns>
        private static string GetAttackID(string stateName)
        {
            if (stateName.ToLower().Contains("specialn") || stateName.ToLower().Contains("specialairn"))
                return "ATKKIND_SPECIALN";

            if (stateName.ToLower().Contains("specials") || stateName.ToLower().Contains("specialairs"))
                return "ATKKIND_SPECIALS";

            if (stateName.ToLower().Contains("specialhi") || stateName.ToLower().Contains("specialairhi"))
                return "ATKKIND_SPECIALHI";

            if (stateName.ToLower().Contains("speciallw") || stateName.ToLower().Contains("specialairlw"))
                return "ATKKIND_SPECIALLW";

            return "ATKKIND_NONE";
        }

        public string Help()
        {
            return "";
        }

        public string ID()
        {
            return "-mlg";
        }

        public string Name()
        {
            return "Move Logic Generator";
        }
    }
}
