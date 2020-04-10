using System.Collections.Generic;
using static MexFF.ELFTools;

namespace MexFF
{
    /// <summary>
    ///  Holds data scrapted from ELF files
    /// </summary>
    public class ELFContents
    {
        public byte[] Code;

        public List<Symbols> Functions = new List<Symbols>();
        public List<Reloc> Relocs = new List<Reloc>();
    }
}
