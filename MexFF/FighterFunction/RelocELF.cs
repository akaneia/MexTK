using HSDRaw;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MexTK.FighterFunction
{
    public class RelocELF
    {
        public class SymbolData
        {
            public string Symbol;
            public string SectionName;
            public bool External;
            public byte[] Data;
            public List<RelocData> Relocations = new List<RelocData>();

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public List<SymbolData> GetDependencies()
            {
                List<SymbolData> dep = new List<SymbolData>();
                HashSet<SymbolData> hashes = new HashSet<SymbolData>();
                GetDependencies(dep, hashes);
                return dep;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="data"></param>
            /// <param name="hashes"></param>
            private void GetDependencies(List<SymbolData> data, HashSet<SymbolData> hashes)
            {
                if (hashes.Contains(this))
                    return;

                data.Add(this);
                hashes.Add(this);

                foreach (var r in Relocations)
                    r.Symbol.GetDependencies(data, hashes);
            }
        }

        public class RelocData
        {
            public uint Offset;
            public uint AddEnd;
            public SymbolData Symbol;
            public RelocType Type;
            public uint SymbolIndex;

            public override bool Equals(object obj)
            {
                if (obj is RelocData data)
                {
                    return Offset == data.Offset && 
                        AddEnd == data.AddEnd && 
                        Type == data.Type && 
                        Symbol.SectionName == data.Symbol.SectionName;
                }
                return false;
            }
        }

        public class SectionData
        {
            public string Name;
            public byte[] Data;
            public List<RelocData> Relocations = new List<RelocData>();
        }
        
        /// <summary>
        /// 
        /// </summary>
        private List<SymbolData> SymbolSections = new List<SymbolData>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elfFile"></param>
        public RelocELF(byte[] elfFile)
        {
            using (MemoryStream mstream = new MemoryStream(elfFile))
            using (BinaryReaderExt r = new BinaryReaderExt(mstream))
            {
                // Parse Header

                if (!(r.ReadByte() == 0x7F && r.ReadByte() == 0x45 && r.ReadByte() == 0x4C && r.ReadByte() == 0x46))
                    throw new InvalidDataException("Not a valid ELF file");

                byte bitType = r.ReadByte(); // 1 - 32, 2 - 64
                if (bitType != 1)
                    throw new NotSupportedException("Only 32 bit ELF files are currently supported");

                r.BigEndian = r.ReadByte() == 2;

                // I only care about the sections
                r.Seek(0x20);
                var sectionOffset = r.ReadUInt32();
                r.Seek(0x2E);
                var sectionHeaderSize = r.ReadUInt16();
                var numOfSections = r.ReadInt16();
                var StringSectionIndex = r.ReadUInt16();

                List<SectionData> DataSections = new List<SectionData>();

                // Parse Sections
                var Sections = new ELFSection[numOfSections];
                for (uint i = 0; i < numOfSections; i++)
                {
                    r.Seek(sectionOffset + sectionHeaderSize * i);

                    Sections[i] = new ELFSection()
                    {
                        sh_name = r.ReadUInt32(),
                        sh_type = (SectionType)r.ReadInt32(),
                        sh_flags = r.ReadUInt32(),
                        sh_addr = r.ReadUInt32(),
                        sh_offset = r.ReadUInt32(),
                        sh_size = r.ReadUInt32(),
                        sh_link = r.ReadUInt32(),
                        sh_info = r.ReadUInt32(),
                        sh_addralign = r.ReadUInt32(),
                        sh_entsize = r.ReadUInt32()
                    };
                    
                    DataSections.Add(new SectionData());
                }
                
                // Parse Symbols
                var symbolSection = Array.Find(Sections, e => r.ReadString((int)(Sections[StringSectionIndex].sh_offset + e.sh_name), -1) == ".symtab");
                
                var Symbols = new ELFSymbol[symbolSection.sh_size / 0x10];
                for (uint i = 0; i < Symbols.Length; i++)
                {
                    r.Seek(symbolSection.sh_offset + 0x10 * i);

                    Symbols[i] = new ELFSymbol()
                    {
                        st_name = r.ReadUInt32(),
                        st_value = r.ReadUInt32(),
                        st_size = r.ReadUInt32(),
                        st_info = r.ReadByte(),
                        st_other = r.ReadByte(),
                        st_shndx = r.ReadInt16()
                    };

                    SymbolSections.Add(new SymbolData());
                }

                // Grab Relocation Data

                for (int i = 0; i < Sections.Length; i++)
                {
                    var section = Sections[i];

                    var data = DataSections[i];
                    data.Name = r.ReadString((int)(Sections[StringSectionIndex].sh_offset + Sections[i].sh_name), -1);

                    data.Data = r.GetSection(section.sh_offset, (int)section.sh_size);
                    
                    if (section.sh_type == SectionType.SHT_RELA || section.sh_type == SectionType.SHT_REL)
                    {
                        var relocs = ParseRelocationSection(r, section);

                        foreach (var v in relocs)
                        {
                            DataSections[(int)section.sh_info].Relocations.Add(new RelocData()
                            {
                                Offset = v.r_offset,
                                AddEnd = v.r_addend,
                                Symbol = SymbolSections[(int)v.R_SYM],
                                Type = (RelocType)v.R_TYP,
                                SymbolIndex = v.R_SYM
                            });
                        }
                    }
                }

                var symbolStringSection = Sections[symbolSection.sh_link];
                
                // rip out symbol data

                for (int i = 0; i < Symbols.Length; i++)
                {
                    var sym = Symbols[i];

                    var section = sym.st_shndx >= 0 ? DataSections[sym.st_shndx] : null;

                    byte[] symbolData = new byte[sym.st_size];
                    List<RelocData> relocations = new List<RelocData>();

                    if (section != null)
                    {
                        SymbolSections[i].SectionName = section.Name;

                        Array.Copy(section.Data, sym.st_value, symbolData, 0, symbolData.Length);

                        relocations = section.Relocations.Where(e => e.Offset >= sym.st_value && e.Offset < sym.st_value + sym.st_size).ToList();

                        foreach(var rel in relocations)
                            rel.Offset -= sym.st_value;

                        if (symbolData.Length == 0)
                            symbolData = section.Data;

                        SymbolSections[i].External = Sections[sym.st_shndx].sh_offset == 0;

                    }

                    SymbolSections[i].Symbol = r.ReadString((int)(symbolStringSection.sh_offset + sym.st_name), -1);
                    SymbolSections[i].Data = symbolData;
                    SymbolSections[i].Relocations = relocations;
                }

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="r"></param>
        /// <param name="sec"></param>
        /// <returns></returns>
        private ELFRelocA[] ParseRelocationSection(BinaryReaderExt r, ELFSection sec)
        {
            ELFRelocA[] relocA = new ELFRelocA[0];

            if (sec.sh_type == SectionType.SHT_RELA || sec.sh_type == SectionType.SHT_REL)
            {
                uint relSize = 0x0C;
                var count = (sec.sh_size / relSize);

                relocA = new ELFRelocA[count];
                for (uint i = 0; i < count; i++)
                {
                    r.Seek(sec.sh_offset + relSize * i);

                    relocA[i] = new ELFRelocA()
                    {
                        r_offset = r.ReadUInt32(),
                        r_info = r.ReadUInt32(),
                        r_addend = (sec.sh_type == SectionType.SHT_RELA ? r.ReadUInt32() : 0)
                    };
                }
            }
            return relocA;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static HSDAccessor GenerateFunctionDAT(RelocELF[] elfFiles, string[] functions, bool quiet = false)
        {
            // Grab Symbol Table Info
            Dictionary<int, SymbolData> data = new Dictionary<int, SymbolData>();
            Queue<SymbolData> symbolQueue = new Queue<SymbolData>();

            // Gather the root symbols needed for function table
            if(functions == null)
            {
                if (!quiet)
                    Console.WriteLine("No function table entered: defaulting to patching");

                foreach (var elf in elfFiles)
                {
                    foreach(var sym in elf.SymbolSections)
                    {
                        var m = System.Text.RegularExpressions.Regex.Matches(sym.Symbol, @"0[xX][0-9a-fA-F]+");
                        if (m.Count > 0)
                        {
                            uint loc;
                            if (uint.TryParse(m[0].Value.ToLower().Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out loc))
                            {
                                data.Add((int)loc, sym);
                                symbolQueue.Enqueue(sym);
                            }
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < functions.Length; i++)
                {
                    foreach (var elf in elfFiles)
                    {
                        var sym = elf.SymbolSections.Find(e => e.Symbol.Equals(functions[i], StringComparison.InvariantCultureIgnoreCase));
                        if (sym != null)
                        {
                            data.Add(i, sym);
                            symbolQueue.Enqueue(sym);
                        }
                    }
                }
            }

            // Resolve External and Duplicate Symbols
            Dictionary<SymbolData, SymbolData> SymbolRemapper = new Dictionary<SymbolData, SymbolData>();

            // Get All Symbols and Dependencies
            List<SymbolData> usedSymbols = new List<SymbolData>();
            while (symbolQueue.Count > 0)
            {
                var orgsym = symbolQueue.Dequeue();
                var sym = orgsym;

                // check if already remapped
                if (SymbolRemapper.ContainsKey(sym))
                    continue;

                // resolve external and remap
                if (sym.External)
                {
                    foreach (var elf in elfFiles)
                    {
                        var externalSymbol = elf.SymbolSections.Where(e => e.Symbol.Equals(sym.Symbol) && !e.External).ToArray();

                        if (externalSymbol.Length > 0)
                        {
                            SymbolRemapper.Add(sym, externalSymbol[0]);
                            sym = externalSymbol[0];
                            break;
                        }
                    }
                    if (sym == orgsym)
                        throw new Exception("Could not resolve external symbol " + sym.Symbol + " - " + sym.SectionName);
                }

                // resolve duplicates
                if (!string.IsNullOrEmpty(sym.Symbol))
                {
                    bool duplicate = false;
                    // check if a symbol with this name is already used
                    foreach (var s in usedSymbols)
                    {
                        if (s.Symbol.Equals(sym.Symbol))
                        {
                            // remap this symbol and mark as duplicate
                            SymbolRemapper.Add(sym, s);
                            duplicate = true;
                            break;
                        }
                    }
                    // if a duplicate is found, ignore this symbol
                    if (duplicate)
                        continue;
                }

                // add symbols
                usedSymbols.Add(sym);


                // gather relocations from other symbols if necessary
                // transfer relocations??
                // if these are the same symbol and there is supposed to be a relocation, then port it?
                foreach(var el in elfFiles)
                {
                    var symcols = el.SymbolSections.Where(e=>
                        e != sym && 
                        e.SectionName == sym.SectionName &&
                        e.Data.SequenceEqual(sym.Data));
                    
                    foreach(var s in symcols)
                    {
                        foreach (var sReloc in s.Relocations)
                        {
                            var exists = sym.Relocations.Exists(e => e.Equals(sReloc));

                            if (!exists)
                                sym.Relocations.Add(sReloc);
                        }
                    }
                }

                // add relocation sections to queue
                foreach (var v in sym.Relocations)
                {
                    if (!usedSymbols.Contains(v.Symbol) && !symbolQueue.Contains(v.Symbol))
                        symbolQueue.Enqueue(v.Symbol);
                }
            }
            

            // remap relocation table
            foreach (var v in usedSymbols)
            {
                System.Diagnostics.Debug.WriteLine(v.SectionName + " " + v.Relocations.Count);
                for (int i = 0; i < v.Relocations.Count; i++)
                {
                    if (SymbolRemapper.ContainsKey(v.Relocations[i].Symbol))
                        v.Relocations[i].Symbol = SymbolRemapper[v.Relocations[i].Symbol];

                    if (!usedSymbols.Contains(v.Relocations[i].Symbol))
                        throw new Exception("Missing Symbol " + v.Relocations[i].Symbol.Symbol + " " + v.Symbol);
                }

            }


            // Generate Function DAT

            var function = new HSDAccessor() { _s = new HSDStruct(0x14) };

            // Generate code section
            Dictionary<SymbolData, long> dataToOffset = new Dictionary<SymbolData, long>();
            using (MemoryStream code = new MemoryStream())
            {
                foreach(var v in usedSymbols)
                {
                    // align
                    if (code.Length % 4 != 0)
                        code.Write(new byte[4 - (code.Length % 4)], 0, 4 - ((int)code.Length % 4));

                    // write code
                    dataToOffset.Add(v, code.Length);
                    code.Write(v.Data, 0, v.Data.Length);
                }
                function._s.SetReferenceStruct(0x00, new HSDStruct(code.ToArray()));
            }

            // Generate Function Table

            HSDStruct functionTable = new HSDStruct(8);
            var funcCount = 0;
            foreach(var v in data)
            {
                functionTable.Resize(8 * (funcCount + 1));
                functionTable.SetInt32(funcCount * 8, v.Key);
                functionTable.SetInt32(funcCount * 8 + 4, (int)dataToOffset[v.Value]);
                funcCount++;
            }

            function._s.SetReferenceStruct(0x0C, functionTable);
            function._s.SetInt32(0x10, funcCount);

            // Generate Relocation Table

            HSDStruct relocationTable = new HSDStruct(0);
            var relocCount = 0;
            foreach(var v in usedSymbols)
            {
                if(v.Data.Length == 0)
                {
                    throw new Exception($"Error: {v.Symbol} length is {v.Data.Length.ToString("X")}");
                }

                if (!quiet)
                {
                    Console.WriteLine($"{v.Symbol,-30} {v.SectionName, -50} Offset: {dataToOffset[v].ToString("X8"), -16} Length: {v.Data.Length.ToString("X8")}");
                    if(v.Relocations.Count > 0)
                        Console.WriteLine($"\t {"Section:",-50} {"RelocType:",-20} {"FuncOffset:", -16} {"SectionOffset:"}");
                }

                foreach (var reloc in v.Relocations)
                {
                    if (!quiet)
                        Console.WriteLine($"\t {reloc.Symbol.SectionName, -50} {reloc.Type, -20} {reloc.Offset.ToString("X8"), -16} {reloc.AddEnd.ToString("X8")}");
                    relocationTable.Resize((relocCount + 1) * 0x08);
                    relocationTable.SetInt32(0x00 + relocCount * 8, (int)(dataToOffset[v] + reloc.Offset));
                    relocationTable.SetByte(0x00 + relocCount * 8, (byte)reloc.Type);
                    relocationTable.SetInt32(0x04 + relocCount * 8, (int)(dataToOffset[reloc.Symbol] + reloc.AddEnd));
                    relocCount++;

                    switch (reloc.Type)
                    {
                        // currently supported types
                        case RelocType.R_PPC_ADDR32:
                        case RelocType.R_PPC_ADDR16_LO:
                        case RelocType.R_PPC_ADDR16_HA:
                        case RelocType.R_PPC_REL24:
                            break;
                        default:
                            Console.WriteLine("Warning: unsupported reloc type " + reloc.Type.ToString("X"));
                            break;
                    }
                }
            }

            function._s.SetReferenceStruct(0x04, relocationTable);
            function._s.SetInt32(0x08, relocCount);
            
            return function;
        }

    }
}
