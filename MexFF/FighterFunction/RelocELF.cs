using HSDRaw;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public List<SymbolData> SymbolSections = new List<SymbolData>();

        public IEnumerable<SymbolData> SymbolEnumerator
        {
            get
            {
                foreach (var v in SymbolSections)
                    yield return v;
            }
        }

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

                        if (Sections[sym.st_shndx].sh_type == SectionType.SHT_NOBITS)
                        {
                            symbolData = new byte[section.Data.Length];
#if DEBUG
                            // Console.WriteLine($"{section.Name} {(Sections[sym.st_shndx].sh_offset + sym.st_value).ToString("X")} {sym.st_size} {sym.st_value} {symbolData.Length} {Sections[sym.st_shndx].sh_type}");
#endif
                        }
                        else
                        {
                            // If size of section is 0, get all data?
                            if (sym.st_size == 0)
                                symbolData = section.Data;
                            //else
                            //if ((sym.st_value & 0x80000000) != 0)
                            //{
                            //    Array.Copy(section.Data, sym.st_value - 0x80000000 - Sections[sym.st_shndx].sh_offset, symbolData, 0, sym.st_size);
                            //    Debug.WriteLine($"LONG CALL {section.Relocations.Count} Off: {(sym.st_value - 0x80000000).ToString("X")} SectionOff: {Sections[sym.st_shndx].sh_offset.ToString("X")} {sym.st_value.ToString("X")} Size: {sym.st_size.ToString("X")} Total Size: {section.Data.Length.ToString("X")}");
                            //}
                            else
                                Array.Copy(section.Data, sym.st_value, symbolData, 0, sym.st_size);

                            // TODO: when to get relocations?
                            relocations = section.Relocations.Where(
                                e => e.Offset >= sym.st_value &&
                                (e.Offset < sym.st_value + symbolData.Length)
                            ).ToList();

                            // make relative
                            foreach (var rel in relocations)
                                rel.Offset -= sym.st_value;
                        }

                        // if the offset is 0 the function is usually in another file
                        SymbolSections[i].External = Sections[sym.st_shndx].sh_offset == 0;
#if DEBUG
                        //Console.WriteLine(section.Name + " " + r.ReadString((int)(symbolStringSection.sh_offset + sym.st_name), -1)
                        //    + " " + Sections[sym.st_shndx].sh_info + " " + Sections[sym.st_shndx].sh_addr + " " + relocations.Count);

                        //Debug.WriteLine($"{section.Name} {r.ReadString((int)(symbolStringSection.sh_offset + sym.st_name), -1)} {(Sections[sym.st_shndx].sh_offset + + sym.st_value).ToString("X")} {sym.st_size.ToString("X")}");


                        if (section.Name == ".debug_line")
                        {
                            //r.Seek(Sections[sym.st_shndx].sh_offset + +sym.st_value);
                            //ParseDebugLine(r);
                        }

#endif
                    }

                    SymbolSections[i].Symbol = r.ReadString((int)(symbolStringSection.sh_offset + sym.st_name), -1);
                    SymbolSections[i].Data = symbolData;
                    SymbolSections[i].Relocations = relocations;

                }

            }
        }

        private static void ParseDebugLine(BinaryReaderExt r)
        {
            // read header
            var debug_line_header = new ELF_Debug_Line()
            {
                length = r.ReadUInt32(),
                version = r.ReadUInt16(),
                header_length = r.ReadUInt32(),
                min_instruction_length = r.ReadByte(),
                default_is_stmt = r.ReadByte(),
                line_base = r.ReadSByte(),
                line_range = r.ReadByte(),
                opcode_base = r.ReadByte(),
                std_opcode_lengths = new byte[12]
            };
            for (int k = 0; k < 12; k++)
                debug_line_header.std_opcode_lengths[k] = r.ReadByte();

            // read directories
            while (r.PeekChar() != 0)
            {
                Debug.WriteLine(r.ReadString());
            }
            r.Skip(1);

            // read files
            while (r.PeekChar() != 0)
            {
                Debug.WriteLine(r.ReadString() + " " + r.ReadByte() + " " + r.ReadByte() + " " + r.ReadByte());
            }

            r.PrintPosition();

            //int address = 0;
            //int op_index = 0;
            //int file = 1;
            //int line = 1;
            //int column = 0;
            //bool is_stmt = debug_line_header.default_is_stmt != 0;
            //bool basic_block = false;
            //bool end_sequence = false;
            //bool prologue_end = false;
            //bool epilogue_begin = false;
            //int isa = 0;
            //int dicriminator = 0;

            var op_code_start = r.Position;

            while (true)
            {
                var op = r.ReadByte();

                switch (op)
                {
                    case 0:
                        // extended byte
                        ReadLEB123(r);
                        break;
                }
            }
        }

        public static int ReadLEB123(BinaryReaderExt e)
        {
            int result = 0;
            int shift = 0;
            while (true)
            {
                byte b = e.ReadByte();
                result |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                    break;
                shift += 7;
            }
            return result;
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

    }
}
