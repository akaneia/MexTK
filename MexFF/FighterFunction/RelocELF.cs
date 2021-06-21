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
        private List<SymbolData> SymbolSections = new List<SymbolData>();

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

                        // If size of section is 0, get all data?
                        if (Sections[sym.st_shndx].sh_type == SectionType.SHT_NOBITS)
                        {
                            symbolData = new byte[section.Data.Length];
#if DEBUG
                            Console.WriteLine($"{section.Name} {(Sections[sym.st_shndx].sh_offset + sym.st_value).ToString("X")} {sym.st_size} {sym.st_value} {symbolData.Length}");
#endif
                        }
                        else
                        {
                            if (sym.st_size == 0)
                                symbolData = section.Data;
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
                        Console.WriteLine(section.Name + " " + r.ReadString((int)(symbolStringSection.sh_offset + sym.st_name), -1)
                            + " " + Sections[sym.st_shndx].sh_info + " " + Sections[sym.st_shndx].sh_addr + " " + relocations.Count);

                        Debug.WriteLine($"{section.Name} {r.ReadString((int)(symbolStringSection.sh_offset + sym.st_name), -1)} {(Sections[sym.st_shndx].sh_offset + + sym.st_value).ToString("X")} {sym.st_size.ToString("X")}");


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

            int address = 0;
            int op_index = 0;
            int file = 1;
            int line = 1;
            int column = 0;
            bool is_stmt = debug_line_header.default_is_stmt != 0;
            bool basic_block = false;
            bool end_sequence = false;
            bool prologue_end = false;
            bool epilogue_begin = false;
            int isa = 0;
            int dicriminator = 0;

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

        public class LinkedELF
        {
            public Dictionary<string, SymbolData> SymbolToData = new Dictionary<string, SymbolData>();

            public List<SymbolData> AllSymbols = new List<SymbolData>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elfFiles"></param>
        /// <param name="linkFiles"></param>
        /// <param name="functions"></param>
        /// <param name="quiet"></param>
        public static LinkedELF LinkELFs(RelocELF[] elfFiles, LinkFile linkFiles, string[] functions, bool quiet = false)
        {
            LinkedELF lelf = new LinkedELF();

            // Grab Symbol Table Info
            Queue<SymbolData> symbolQueue = new Queue<SymbolData>();

            // Gather the root symbols needed for function table
            if (functions == null)
            {
                if (!quiet)
                    Console.WriteLine("No function table entered: defaulting to patching");

                foreach (var elf in elfFiles)
                {
                    foreach (var sym in elf.SymbolSections)
                    {
                        var m = System.Text.RegularExpressions.Regex.Matches(sym.Symbol, @"0[xX][0-9a-fA-F]+");
                        if (m.Count > 0)
                        {
                            uint loc;
                            if (uint.TryParse(m[0].Value.ToLower().Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out loc))
                            {
                                lelf.SymbolToData.Add(loc.ToString("X"), sym);
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
                            lelf.SymbolToData.Add(functions[i], sym);
                            symbolQueue.Enqueue(sym);
                        }
                    }
                }
            }

            // Resolve External and Duplicate Symbols
            Dictionary<SymbolData, SymbolData> SymbolRemapper = new Dictionary<SymbolData, SymbolData>();

            // Get All Symbols and Dependencies
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
                    bool found = false;

                    foreach (var elf in elfFiles)
                    {
                        var externalSymbol = elf.SymbolSections.Where(e => e.Symbol.Equals(sym.Symbol) && !e.External).ToArray();

                        if (externalSymbol.Length > 0)
                        {
                            found = true;
                            SymbolRemapper.Add(sym, externalSymbol[0]);
                            sym = externalSymbol[0];
                            break;
                        }
                    }

                    if (linkFiles.ContainsSymbol(sym.Symbol))
                        found = true;

                    if (!found && sym.Symbol != "_GLOBAL_OFFSET_TABLE_")
                        throw new Exception("Could not resolve external symbol " + sym.Symbol + " - " + sym.SectionName);
                }

                // resolve duplicates
                if (!string.IsNullOrEmpty(sym.Symbol))
                {
                    bool duplicate = false;
                    // check if a symbol with this name is already used
                    foreach (var s in lelf.AllSymbols)
                    {
                        if (s.Symbol.Equals(sym.Symbol))
                        {
                            // remap this symbol and mark as duplicate
                            if(!SymbolRemapper.ContainsKey(sym))
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
                lelf.AllSymbols.Add(sym);


                // gather relocations from other symbols if necessary
                // transfer relocations??
                // if these are the same symbol and there is supposed to be a relocation, then port it?
                foreach (var el in elfFiles)
                {
                    var symcols = el.SymbolSections.Where(e =>
                        e != sym &&
                        e.SectionName == sym.SectionName &&
                        e.Data.SequenceEqual(sym.Data));

                    foreach (var s in symcols)
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
                    if (!lelf.AllSymbols.Contains(v.Symbol) && !symbolQueue.Contains(v.Symbol))
                        symbolQueue.Enqueue(v.Symbol);
                }
            }


            // remap relocation table
            foreach (var v in lelf.AllSymbols)
            {
                System.Diagnostics.Debug.WriteLine(v.SectionName + " " + v.Relocations.Count);
                for (int i = 0; i < v.Relocations.Count; i++)
                {
                    if (SymbolRemapper.ContainsKey(v.Relocations[i].Symbol))
                        v.Relocations[i].Symbol = SymbolRemapper[v.Relocations[i].Symbol];

                    if (!lelf.AllSymbols.Contains(v.Relocations[i].Symbol) && !linkFiles.ContainsSymbol(v.Relocations[i].Symbol.Symbol))
                        throw new Exception("Missing Symbol " + v.Relocations[i].Symbol.Symbol + " " + v.Symbol);
                }
            }

            return lelf;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static HSDAccessor GenerateFunctionDAT(RelocELF[] elfFiles, LinkFile linkFiles, string[] functions, bool debug, bool quiet = false)
        {
            var lelf = LinkELFs(elfFiles, linkFiles, functions, quiet);

            // Generate Function DAT
            var function = new HSDAccessor() { _s = new HSDStruct(0x20) };

            // Generate code section
            HSDStruct debug_symbol_table = null;
            int debug_symbol_count = 0;
            Dictionary<SymbolData, long> dataToOffset = new Dictionary<SymbolData, long>();
            byte[] codedata;
            using (MemoryStream code = new MemoryStream())
            {
                if (debug)
                    debug_symbol_table = new HSDStruct((lelf.AllSymbols.Count + 1) * 0xC);
                int function_index = 0;
                foreach(var v in lelf.AllSymbols)
                {
                    // align
                    if (code.Length % 4 != 0)
                        code.Write(new byte[4 - (code.Length % 4)], 0, 4 - ((int)code.Length % 4));

                    int code_start = (int)code.Position;
                    // write code
                    if(v.Data.Length == 0 && linkFiles.TryGetSymbolAddress(v.Symbol, out uint addr))
                    {
                        dataToOffset.Add(v, addr);
                    }
                    else
                    {
                        dataToOffset.Add(v, code.Length);
                        code.Write(v.Data, 0, v.Data.Length);
                    }
                    int code_end = (int)code.Position;

                    if (debug && code_start != code_end)
                    {
                        debug_symbol_table.SetInt32(function_index * 0xC, code_start);
                        debug_symbol_table.SetInt32(function_index * 0xC + 4, code_end);
                        debug_symbol_table.SetString(function_index * 0xC + 8, v.Symbol, true);
                        debug_symbol_count++;
                    }

                    function_index++;
                }
                codedata = code.ToArray();
            }

            // generate function table
            HSDStruct functionTable = new HSDStruct(8);
            var funcCount = 0;
            var fl = functions.ToList();
            foreach(var v in lelf.SymbolToData)
            {
                functionTable.Resize(8 * (funcCount + 1));
                functionTable.SetInt32(funcCount * 8, fl.IndexOf(v.Key));
                functionTable.SetInt32(funcCount * 8 + 4, (int)dataToOffset[v.Value]);
                funcCount++;
            }


            // set function table
            function._s.SetReferenceStruct(0x0C, functionTable);
            function._s.SetInt32(0x10, funcCount);


            // Generate Relocation Table
            HSDStruct relocationTable = new HSDStruct(0);
            var relocCount = 0;
            foreach(var v in lelf.AllSymbols)
            {
                // check data length
                if (v.Data.Length == 0)
                    if (linkFiles.ContainsSymbol(v.Symbol))
                        continue;
                    else
                        throw new Exception($"Error: {v.Symbol} length is {v.Data.Length.ToString("X")}");

                // print debug info
                if (!quiet)
                {
                    Console.WriteLine($"{v.Symbol,-30} {v.SectionName, -50} Offset: {dataToOffset[v].ToString("X8"), -16} Length: {v.Data.Length.ToString("X8")}");
                    if(v.Relocations.Count > 0)
                        Console.WriteLine($"\t {"Section:",-50} {"RelocType:",-20} {"FuncOffset:", -16} {"SectionOffset:"}");
                }
                
                // process and create relocation table
                foreach (var reloc in v.Relocations)
                {
                    if (!quiet)
                        Console.WriteLine($"\t {reloc.Symbol.SectionName, -50} {reloc.Type, -20} {reloc.Offset.ToString("X8"), -16} {reloc.AddEnd.ToString("X8")}");
                    
                    // gather code positions
                    var codeOffset = (int)(dataToOffset[v] + reloc.Offset);
                    var toFunctionOffset = (int)(dataToOffset[reloc.Symbol] + reloc.AddEnd);

                    // currently supported types check
                    switch (reloc.Type)
                    {
                        case RelocType.R_PPC_REL32:
                        case RelocType.R_PPC_REL24:
                        case RelocType.R_PPC_ADDR32:
                        case RelocType.R_PPC_ADDR16_LO:
                        case RelocType.R_PPC_ADDR16_HA:
                            break;
                        default:
                            // no exception, but not guarenteed to work
                            Console.WriteLine("Warning: unsupported reloc type " + reloc.Type.ToString("X") + " send this to Ploaj or UnclePunch");
                            break;
                    }

                    bool addEntry = true;

                    // only apply optimization if not external
                    if (!reloc.Symbol.External)
                    {
                        // calculate relative offset
                        var rel = toFunctionOffset - codeOffset;

                        // apply relocation automatically if possible
                        switch (reloc.Type)
                        {
                            case RelocType.R_PPC_REL32:
                                codedata[codeOffset] = (byte)((rel >> 24) & 0xFF);
                                codedata[codeOffset + 1] = (byte)((rel >> 16) & 0xFF);
                                codedata[codeOffset + 2] = (byte)((rel >> 8) & 0xFF);
                                codedata[codeOffset + 3] = (byte)((rel) & 0xFF);

                                addEntry = false;
                                break;
                            case RelocType.R_PPC_REL24:
                                var cur = ((codedata[codeOffset] & 0xFF) << 24) | ((codedata[codeOffset + 1] & 0xFF) << 16) | ((codedata[codeOffset + 2] & 0xFF) << 8) | ((codedata[codeOffset + 3] & 0xFF));
                                rel = cur | (rel & 0x03FFFFFC);

                                codedata[codeOffset] = (byte)((rel >> 24) & 0xFF);
                                codedata[codeOffset + 1] = (byte)((rel >> 16) & 0xFF);
                                codedata[codeOffset + 2] = (byte)((rel >> 8) & 0xFF);
                                codedata[codeOffset + 3] = (byte)((rel) & 0xFF);

                                addEntry = false;
                                break;
                        }
                    }

                    // add relocation to table
                    if(addEntry)
                    {
                        relocationTable.Resize((relocCount + 1) * 0x08);
                        relocationTable.SetInt32(0x00 + relocCount * 8, codeOffset);
                        relocationTable.SetByte(0x00 + relocCount * 8, (byte)reloc.Type);
                        relocationTable.SetInt32(0x04 + relocCount * 8, toFunctionOffset);

                        relocCount++;
                    }
                }
            }
            
            function._s.SetReferenceStruct(0x00, new HSDStruct(codedata));
            function._s.SetReferenceStruct(0x04, relocationTable);
            function._s.SetInt32(0x08, relocCount);

            if (debug_symbol_table != null)
            {
                function._s.SetInt32(0x14, codedata.Length);
                function._s.SetInt32(0x18, debug_symbol_count);
                function._s.SetReferenceStruct(0x1C, debug_symbol_table);
            }

            return function;
        }

    }
}
