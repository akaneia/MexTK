using HSDRaw;

namespace MexTK.Commands
{
    public class CmdTrimDAT : ICommand
    {
        public bool DoIt(string[] args)
        {
            if (args.Length >= 2)
            {
                var dat = args[1];

                HSDRawFile file = new HSDRawFile(dat);
                foreach (var r in file.Roots)
                    r.Data.Optimize();
                file.Save(dat);

                return true;
            }

            return false;
        }

        public string Help()
        {
            return @"MexTK.exe -trim (dat file)";
        }

        public string ID()
        {
            return "-trim";
        }

        public string Name()
        {
            return "Trim and Optimize DAT";
        }
    }
}
