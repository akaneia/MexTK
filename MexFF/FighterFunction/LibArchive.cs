using System.Collections.Generic;
using System.IO;

namespace MexTK.FighterFunction
{
    public class LibArchive
    {
        public static List<RelocELF> GetElfs(string filePath)
        {
            List<RelocELF> elfs = new List<RelocELF>();

            using (FileStream s = new FileStream(filePath, FileMode.Open))
            using (BinaryReader r = new BinaryReader(s))
            {
                if (s.Length < 8)
                    return elfs;

                if (!new string(r.ReadChars(7)).Equals("!<arch>"))
                    return elfs;

                if (r.ReadByte() != 0x0A)
                    return elfs;

                // parse headers until end of file
                while (s.Position < s.Length)
                {
                    var identifier = new string(r.ReadChars(16)).TrimEnd().TrimEnd('/');
                    var modstamp = new string(r.ReadChars(12)).TrimEnd();
                    int.TryParse(new string(r.ReadChars(6)).TrimEnd(), out int owner);
                    int.TryParse(new string(r.ReadChars(6)).TrimEnd(), out int group);
                    int.TryParse(new string(r.ReadChars(8)).TrimEnd(), out int mode);
                    int.TryParse(new string(r.ReadChars(10)).TrimEnd(), out int size);
                    r.ReadChars(2);

                    if (Path.GetExtension(identifier).Equals(".o"))
                        elfs.Add(new RelocELF(r.ReadBytes(size)));
                    else
                        s.Position += size;
                }
            }

            return elfs;
        }
    }
}
