using System;
using System.Collections.Generic;
using System.IO;
using HSDRaw;
using System.Linq;
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

            public bool External;
        }

        public class Reloc
        {
            public uint RelocOffset;

            public uint FunctionOffset;

            public RelocType Flag;
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
            // scrape all elfs
            /*var buildFolder = Path.GetDirectoryName(elfFile);
            ELFContents mainELF = null;
            List<ELFContents> Contents = new List<ELFContents>();
            foreach(var f in Directory.GetFiles(buildFolder))
            {
                var cont = GetELFContents(f, funcTable, quiet);
                if(f == elfFile)
                    mainELF = cont;
                else
                    Contents.Add(cont);
            }
            if(mainELF == null)
            {
                throw new FileNotFoundException($"{elfFile} not found");
            }


            // find all elf files referenced by main file
            List<ELFContents> UsedContents = new List<ELFContents>();
            Queue<ELFContents> scan = new Queue<ELFContents>();
            scan.Enqueue(mainELF);
            while(scan.Count > 0)
            {
                var cont = scan.Dequeue();
                UsedContents.Add(cont);
                Contents.Remove(cont);
                foreach(var func in cont.Functions)
                {
                    if (func.External)
                    {
                        // search for this function in another elf file
                        foreach (var c in Contents)
                        {
                            if (c.Functions.Any(e => e.Name.Equals(func.Name)))
                                scan.Enqueue(c);
                        }
                    }
                }
            }

            Console.WriteLine($"Linking {UsedContents.Count} ELF files");*/

            // link and build one file
            var mainELF = GetELFContents(elfFile, funcTable, quiet);
            List<byte> code = new List<byte>();
            code.AddRange(mainELF.Code);
            List<Symbols> Functions = mainELF.Functions;
            List<Reloc> Relocs = mainELF.Relocs;
            

            // generate function dat
            HSDStruct functionTable = new HSDStruct(8);
            int funcCount = 0;

            if (!quiet)
                Console.WriteLine("Functions:");

            foreach (var v in Functions)
            {
                if (!string.IsNullOrEmpty(v.Name) && v.SectionIndex > 0)
                {
                    if (funcTable == null)
                    {
                        var m = System.Text.RegularExpressions.Regex.Matches(v.Name, @"0[xX][0-9a-fA-F]+");
                        if (m.Count > 0)
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
                    }
                    else
                    if (funcTable.ContainsKey(v.Name.ToLower()))
                    {
                        functionTable.Resize(8 * (funcCount + 1));
                        functionTable.SetInt32(funcCount * 8, (int)funcTable[v.Name.ToLower()]);
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
                if (v.RelocOffset < code.Count)
                {
                    relocCount++;
                    relocationTable.Resize(relocCount * 0x08);
                    relocationTable.SetInt32(0x00 + (relocCount - 1) * 8, (int)v.RelocOffset);
                    relocationTable.SetByte(0x00 + (relocCount - 1) * 8, (byte)v.Flag);
                    relocationTable.SetInt32(0x04 + (relocCount - 1) * 8, (int)v.FunctionOffset);
                    if (!quiet)
                        Console.WriteLine("\t{0, -10} | {1, -10} | {2, -10}",
                        v.RelocOffset.ToString("X"),
                        v.FunctionOffset.ToString("X"),
                        v.Flag);

                    if (v.Flag != RelocType.R_PPC_ADDR16_HA && v.Flag != RelocType.R_PPC_ADDR16_LO && v.Flag != RelocType.R_PPC_ADDR32 && v.Flag != RelocType.R_PPC_REL32)
                        throw new NotSupportedException("Relocation Flag Type " + v.Flag.ToString("X") + " not supported");

                }
            }


            var function = new HSDAccessor() { _s = new HSDStruct(0x14) };

            function._s.SetReferenceStruct(0x00, new HSDStruct(code.ToArray()));
            function._s.SetReferenceStruct(0x04, relocationTable);
            function._s.SetInt32(0x08, relocCount);
            function._s.SetReferenceStruct(0x0C, functionTable);
            function._s.SetInt32(0x10, funcCount);

            return function;
        }

        /// <summary>
        /// Scrapes data we want from ELF files
        /// </summary>
        /// <param name="elfFile"></param>
        /// <param name="funcTable"></param>
        /// <param name="quiet"></param>
        /// <returns></returns>
        private static ELFContents GetELFContents(string elfFile, Dictionary<string, uint> funcTable, bool quiet = false)
        {
            // function table
            var fncTable = funcTable;
            ELFContents contents = new ELFContents();

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
                
                foreach (var sec in sections)
                {
                    if (!quiet)
                        Console.WriteLine(String.Format("{0, -30} |Type: {5, -15} |Offset: {3, -10} |Size: {4, -10} |Link: {1, -10} |Info: {2, -40}|",
                            r.ReadString((int)(sectionStrings.sh_offset + sec.sh_name), -1),
                            r.ReadString((int)(sectionStrings.sh_offset + sections[sec.sh_link].sh_name), -1),
                            sec.sh_info < sections.Length ? r.ReadString((int)(sectionStrings.sh_offset + sections[sec.sh_info].sh_name), -1) : "",
                            sec.sh_offset.ToString("X"),
                            sec.sh_size.ToString("X"),
                            sec.sh_type));

                    if (sec.sh_type == SectionType.SHT_RELA || sec.sh_type == SectionType.SHT_REL)
                    {
                        uint relSize = 0x0C;
                        var count = (sec.sh_size / relSize);

                        ELFRelocA[] relocA = new ELFRelocA[count];
                        for (uint i = 0; i < count; i++)
                        {
                            r.Seek(sec.sh_offset + relSize * i);

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

                            if (symbol.st_shndx >= 0)
                            {
                                contents.Relocs.Add(new Reloc()
                                {
                                    RelocOffset = sections[sec.sh_info].sh_offset + relocA[i].r_offset,
                                    FunctionOffset = symbol.st_value + sections[symbol.st_shndx].sh_offset + relocA[i].r_addend,
                                    Flag = (RelocType)relocA[i].R_TYP,

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
                            else
                            {
                                Console.WriteLine("Warning: negative symbol index detected " + symbol.st_shndx.ToString("X") + " relocation entry skipped");

                                if ((symbol.st_info & 0xF) != 0x4)
                                    throw new NotSupportedException("This symbol relocation type is not currently supported");

                            }


                        }
                    }
                }

                var symbols = Array.Find(sections, e => r.ReadString((int)(sectionStrings.sh_offset + e.sh_name), -1) == ".symtab");
                var symbolStrings = Array.Find(sections, e => r.ReadString((int)(sectionStrings.sh_offset + e.sh_name), -1) == ".strtab");

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

                    var external = (sec.sh_offset == 0 && !(sym.st_name == 0 || sym.st_shndx < 0));

                    if (external)
                    {
                        //throw new NotSupportedException($"External function reference to \"{funcName}\" in section \"{r.ReadString((int)(sectionStrings.sh_offset + sec.sh_name), -1)}\" not supported!");
                    }
                    if (!(sym.st_name == 0 || sym.st_shndx < 0))
                    {
                        codeStart = Math.Min(sec.sh_offset + sym.st_value, codeStart);
                        codeEnd = Math.Max(sec.sh_size + sec.sh_offset + sym.st_value, codeEnd);
                    }

                    Console.WriteLine((sec.sh_offset + sym.st_value).ToString("X") + " " + sym.st_size.ToString("X") + " " + funcName);

                    contents.Functions.Add(new Symbols()
                    {
                        Name = funcName,
                        CodeOffset = sec.sh_offset + sym.st_value,
                        SectionIndex = sym.st_shndx,
                        External = external
                    });
                }

                contents.Code = r.GetSection(codeStart, (int)(codeEnd - codeStart));

                foreach (var v in contents.Functions)
                {
                    v.CodeOffset -= codeStart;
                }
                foreach (var v in contents.Relocs)
                {
                    v.FunctionOffset -= codeStart;
                    v.RelocOffset -= codeStart;
                }
            }
            return contents;
        }


    }
}