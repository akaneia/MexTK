using System;
using System.Collections.Generic;
using System.IO;
using HSDRaw;
using System.Diagnostics;
using System.Globalization;

namespace MexFF
{
    public class ELFTools
    {
        public class Symbols
        {
            public string Name;

            public uint CodeOffset;

            public int SectionIndex;
        }

        public class Reloc
        {
            public uint RelocOffset;

            public uint FunctionOffset;

            public uint Flag;
        }

        public static HSDAccessor CToDAT(string cFile, Dictionary<string, uint> funcTable = null, bool quiet = false)
        {
            // prepare
            //var tempfile = Path.GetTempFileName();
            var temp = Path.Combine(Path.GetDirectoryName(cFile), "Makefile");

            //if (File.Exists(temp))
            //   throw new Exception("Makefile already exists in directory, please move or delete it and try again");
            if(!File.Exists(temp))
                File.WriteAllText(temp, MakeFile.MFILE);

            Process p = new Process();

            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(cFile);
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = $"make";
            //p.StartInfo.Arguments = $"-f {Path.GetFileName(tempfile)} -C \"{Path.GetDirectoryName(tempfile)}\"";
            p.Start();

            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                Console.WriteLine();
                throw new InvalidDataException($"{Path.GetFileName(cFile)} failed to compile, see output above for details");
            }

            var elf = Path.Combine(Path.Combine(Path.GetDirectoryName(cFile), "build"), Path.GetFileNameWithoutExtension(cFile) + ".o");
            var elfd = Path.Combine(Path.Combine(Path.GetDirectoryName(cFile), "build"), Path.GetFileNameWithoutExtension(cFile) + ".d");

            if (!File.Exists(elf))
                throw new Exception("Failed to compile; see output");
            if (File.Exists(elfd))
                File.Delete(elfd);

            return ELFToDAT(elf, funcTable, quiet);

            // Cleanup
            //File.Delete(tempfile);
        }

        public static HSDAccessor ELFToDAT(string elfFile, Dictionary<string, uint> funcTable = null, bool quiet = false)
        {
            // function table
            var fncTable = funcTable;
            //if (fncTable == null)
           //     fncTable = ftFunctionStrings;

            // parse elf file
            using (BinaryReaderExt r = new BinaryReaderExt(new FileStream(elfFile, FileMode.Open)))
            {
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
                var sectionEntryStringIndex = r.ReadUInt16();

                ELFSection[] sections = new ELFSection[numOfSections];

                // sections
                for (uint i = 0; i < numOfSections; i++)
                {
                    r.Seek(sectionOffset + sectionHeaderSize * i);

                    sections[i] = new ELFSection()
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
                        sh_entsize = r.ReadUInt32(),
                    };
                }

                var sectionStrings = sections[sectionEntryStringIndex];

                // .rela, sh_info => to parent sections | sh_link => symbol map
                // .symtab sh_info => debug line |sh_link => string table
                // check .symtab to find all functions
                // for size of code, consider all SHT_PROGBITS that aren't debug, eh_frame, or comment
                // or for size of code just check symbol sections?

                List<Reloc> Relocs = new List<Reloc>();
                foreach (var sec in sections)
                {
                    if (!quiet)
                        Console.WriteLine(String.Format("{0, -30} |Type: {5, -15} |Offset: {3, -10} |Size: {4, -10} |Link: {1, -10} |Info: {2, -40}|",
                            r.ReadString((int)(sectionStrings.sh_offset + sec.sh_name), -1),
                            r.ReadString((int)(sectionStrings.sh_offset + sections[sec.sh_link].sh_name), -1),
                            r.ReadString((int)(sectionStrings.sh_offset + sections[sec.sh_info].sh_name), -1),
                            sec.sh_offset.ToString("X"),
                            sec.sh_size.ToString("X"),
                            sec.sh_type));

                    if (sec.sh_type == SectionType.SHT_RELA || sec.sh_type == SectionType.SHT_REL)
                    {
                        var count = (sec.sh_size / 0x0C);

                        ELFRelocA[] relocA = new ELFRelocA[count];
                        for (uint i = 0; i < count; i++)
                        {
                            r.Seek(sec.sh_offset + 0x0C * i);

                            relocA[i] = new ELFRelocA()
                            {
                                r_offset = r.ReadUInt32(),
                                r_info = r.ReadUInt32(),
                                r_addend = (sec.sh_type == SectionType.SHT_RELA ? r.ReadUInt32() : 0)
                            };

                            var symMap = sections[sec.sh_link];
                            var symStr = sections[symMap.sh_link];

                            r.Seek(symMap.sh_offset + 0x10 * (uint)relocA[i].R_SYM);
                            var symbol = new ELFSymbol()
                            {
                                st_name = r.ReadUInt32(),
                                st_value = r.ReadUInt32(),
                                st_size = r.ReadUInt32(),
                                st_info = r.ReadByte(),
                                st_other = r.ReadByte(),
                                st_shndx = r.ReadInt16()
                            };

                            Relocs.Add(new Reloc()
                            {
                                RelocOffset = sections[sec.sh_info].sh_offset + relocA[i].r_offset,
                                FunctionOffset = sections[symbol.st_shndx].sh_offset + relocA[i].r_addend,
                                Flag = relocA[i].R_TYP
                            });

                            if (!quiet)
                                Console.WriteLine(
                                String.Format(
                                    "\t\t\t\t\t\t{0, -25} | {1, -10} | {2, -10} | {3, -10}",
                                    r.ReadString((int)(symStr.sh_offset + symbol.st_name), -1),
                                    r.ReadString((int)(sectionStrings.sh_offset + sections[symbol.st_shndx].sh_name), -1),
                                    (sections[sec.sh_info].sh_offset + relocA[i].r_offset).ToString("X"),
                                    relocA[i].R_TYP.ToString("X"),
                                    r.ReadString((int)(sectionStrings.sh_offset + sections[sec.sh_info].sh_name), -1))
                                + " " + relocA[i].r_addend.ToString("X"));

                        }
                    }
                }


                var symbols = Array.Find(sections, e => r.ReadString((int)(sectionStrings.sh_offset + e.sh_name), -1) == ".symtab");
                var symbolStrings = Array.Find(sections, e => r.ReadString((int)(sectionStrings.sh_offset + e.sh_name), -1) == ".strtab");

                List<Symbols> Functions = new List<Symbols>();
                uint codeStart = uint.MaxValue;
                uint codeEnd = 0;
                for (uint i = 0; i < symbols.sh_size / 0x10; i++)
                {
                    r.Seek(symbols.sh_offset + 0x10 * i);

                    var sym = new ELFSymbol()
                    {
                        st_name = r.ReadUInt32(),
                        st_value = r.ReadUInt32(),
                        st_size = r.ReadUInt32(),
                        st_info = r.ReadByte(),
                        st_other = r.ReadByte(),
                        st_shndx = r.ReadInt16()
                    };

                    var sec = sections[sym.st_shndx >= 0 ? sym.st_shndx : 0];

                    var funcName = r.ReadString((int)(symbolStrings.sh_offset + sym.st_name), -1);

                    if (sec.sh_offset == 0 && !(sym.st_name == 0 || sym.st_shndx < 0))
                    {
                        throw new NotSupportedException($"External function reference to \"{funcName}\" in section \"{r.ReadString((int)(sectionStrings.sh_offset + sec.sh_name), -1)}\" not supported!");
                    }
                    if (!(sym.st_name == 0 || sym.st_shndx < 0))
                    {
                        codeStart = Math.Min(sec.sh_offset, codeStart);
                        codeEnd = Math.Max(sec.sh_size + sec.sh_offset, codeEnd);
                    }

                    Functions.Add(new Symbols()
                    {
                        Name = funcName,
                        CodeOffset = sec.sh_offset,
                        SectionIndex = sym.st_shndx
                    });
                }

                // Building RDAT

                byte[] code = r.GetSection(codeStart, (int)(codeEnd - codeStart));

                HSDStruct functionTable = new HSDStruct(8);
                int funcCount = 0;

                if (!quiet)
                    Console.WriteLine("Functions:");

                foreach (var v in Functions)
                {
                    v.CodeOffset -= codeStart;
                    if (!string.IsNullOrEmpty(v.Name) && v.SectionIndex > 0)
                    {
                        if(fncTable == null)
                        {
                            var m = System.Text.RegularExpressions.Regex.Matches(v.Name, @"0[xX][0-9a-fA-F]+");
                            if(m.Count > 0)
                            {
                                uint loc;
                                if (uint.TryParse(m[0].Value.ToLower().Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out loc))
                                {
                                    Console.WriteLine("Overloading ->" + m[0].Value + " : " + v.Name);
                                    functionTable.Resize(8 * (funcCount + 1));
                                    functionTable.SetInt32(funcCount * 8, (int)loc);
                                    functionTable.SetInt32(funcCount * 8 + 4, (int)v.CodeOffset);
                                    funcCount++;
                                }
                            }
                        }else
                        if (fncTable.ContainsKey(v.Name.ToLower()))
                        {
                            functionTable.Resize(8 * (funcCount + 1));
                            functionTable.SetInt32(funcCount * 8, (int)fncTable[v.Name.ToLower()]);
                            functionTable.SetInt32(funcCount * 8 + 4, (int)v.CodeOffset);
                            funcCount++;
                        }

                        if (!quiet)
                            Console.WriteLine($"\t{v.Name,-25} | {v.CodeOffset.ToString("X"),-10}");
                    }
                }
                if (!quiet)
                    Console.WriteLine("Reloc Table:");

                HSDStruct relocationTable = new HSDStruct(0);
                int relocCount = 0;
                foreach (var v in Relocs)
                {
                    v.FunctionOffset -= codeStart;
                    v.RelocOffset -= codeStart;

                    if (v.RelocOffset < code.Length)
                    {
                        if (v.Flag != 0x06 && v.Flag != 0x04 && v.Flag != 0x01)
                            throw new NotSupportedException("Relocation Flag Type " + v.Flag.ToString("X") + " not supported");

                        relocCount++;
                        relocationTable.Resize(relocCount * 0x08);
                        relocationTable.SetInt32(0x00 + (relocCount - 1) * 8, (int)v.RelocOffset);
                        relocationTable.SetByte(0x00 + (relocCount - 1) * 8, (byte)v.Flag);
                        relocationTable.SetInt32(0x04 + (relocCount - 1) * 8, (int)v.FunctionOffset);
                        if (!quiet)
                            Console.WriteLine("\t{0, -10} | {1, -10} | {2, -10}",
                            v.RelocOffset.ToString("X"),
                            v.FunctionOffset.ToString("X"),
                            v.Flag.ToString("X"));
                    }
                }


                var function = new HSDAccessor() { _s = new HSDStruct(0x14) };

                function._s.SetReferenceStruct(0x00, new HSDStruct(code));
                function._s.SetReferenceStruct(0x04, relocationTable);
                function._s.SetInt32(0x08, relocCount);
                function._s.SetReferenceStruct(0x0C, functionTable);
                function._s.SetInt32(0x10, funcCount);

                return function;
            }
        }


        public struct ELFRelocA
        {
            public uint r_offset;
            public uint r_info;
            public uint r_addend;
            public byte R_SYM => (byte)((r_info >> 8) & 0xFF);
            public byte R_TYP => (byte)(r_info & 0xFF);
        }

        public struct ELFSymbol
        {
            public uint st_name;
            public uint st_value;
            public uint st_size;
            public byte st_info;
            public byte st_other;
            public short st_shndx;
        }

        public struct ELFSection
        {
            public uint sh_name;
            public SectionType sh_type;
            public uint sh_flags;
            public uint sh_addr;
            public uint sh_offset;
            public uint sh_size;
            public uint sh_link;
            public uint sh_info;
            public uint sh_addralign;
            public uint sh_entsize;
        }

        public enum SectionType
        {
            SHT_NULL = 0x00,
            SHT_PROGBITS = 0x01,
            SHT_SYMTAB = 0x02,
            SHT_STRTAB = 0x03,
            SHT_RELA = 0x04,
            SHT_HASH = 0x05,
            SHT_DYNAMIC = 0x06,
            SHT_NOTE = 0x07,
            SHT_NOBITS = 0x08,
            SHT_REL = 0x09,
            SHT_SHLIB = 0x0A,
            SHT_DYNSYM = 0x0b,
            SHT_INIT_ARRAY = 0x0E,
            SHT_FINI_ARRAY = 0x0F,
            SHT_PREINIT_ARRAY = 0x10,
            SHT_GROUP = 0x11,
            SHT_SYMTAB_SHNDX = 0x12,
            SHT_NUM = 0x13,
            SHT_LOOS = 0x60000000
        }
    }
}