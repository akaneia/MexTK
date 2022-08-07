using HSDRaw;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static MexTK.FighterFunction.RelocELF;

namespace MexTK.FighterFunction
{
    public class LinkedELF
    {
        private Dictionary<string, SymbolData> SymbolToData = new Dictionary<string, SymbolData>();

        private List<SymbolData> AllSymbols = new List<SymbolData>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elf"></param>
        /// <param name="linkFiles"></param>
        /// <param name="functions"></param>
        /// <param name="quiet"></param>
        public LinkedELF(RelocELF elf, LinkFile linkFiles, string[] functions, bool quiet = false)
        {
            // Grab Symbol Table Info
            Queue<SymbolData> symbolQueue = new Queue<SymbolData>();

            // Gather the root symbols needed for function table
            if (functions == null)
            {
                Console.WriteLine("No function table entered: defaulting to patching");

                foreach (var sym in elf.SymbolSections)
                {
                    var m = System.Text.RegularExpressions.Regex.Matches(sym.Symbol, @"0[xX][0-9a-fA-F]+");
                    if (m.Count > 0)
                    {
                        uint loc;
                        if (uint.TryParse(m[0].Value.ToLower().Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out loc))
                        {
                            SymbolToData.Add(loc.ToString("X"), sym);
                            symbolQueue.Enqueue(sym);
                        }
                    }

                }
            }
            else
            {
                for (int i = 0; i < functions.Length; i++)
                {
                    var sym = elf.SymbolSections.Find(e => CppSanatize(e.Symbol).Equals(functions[i], StringComparison.InvariantCultureIgnoreCase));

                    if (SymbolToData.ContainsKey(functions[i]))
                    {
                        // this error should no longer happen
                        Console.WriteLine($"Warning: found two table functions with the same symbol \"{functions[i]}\" defaulting to first found");
                    }
                    else
                    if (sym != null)
                    {
                        SymbolToData.Add(functions[i], sym);
                        symbolQueue.Enqueue(sym);
                    }
                }
            }

            // Get All Used Symbols and Dependencies
            while (symbolQueue.Count > 0)
            {
                var sym = symbolQueue.Dequeue();

                // check if external symbol is found in link file
                if (sym.External && !linkFiles.ContainsSymbol(CppSanatize(sym.Symbol)))
                    throw new Exception("Could not resolve external symbol " + sym.Symbol + " - " + sym.SectionName + " " + sym.External);

                // inject static addresses from link file
                if (linkFiles.TryGetSymbolAddress(CppSanatize(sym.Symbol), out uint addr) &&
                    sym.Data.Length == 4 &&
                    sym.Data[0] == 0 && sym.Data[1] == 0 && sym.Data[2] == 0 && sym.Data[3] == 0)
                {
                    sym.Data = new byte[] { (byte)((addr >> 24) & 0xFF), (byte)((addr >> 16) & 0xFF), (byte)((addr >> 8) & 0xFF), (byte)(addr & 0xFF) };
                }

                // add symbols to final linked file
                AllSymbols.Add(sym);

                // add relocation sections to queue in order to include dependencies
                foreach (var v in sym.Relocations)
                    if (!AllSymbols.Contains(v.Symbol) && !symbolQueue.Contains(v.Symbol))
                        symbolQueue.Enqueue(v.Symbol);
            }

            // check for missing symbols
            foreach (var v in AllSymbols)
            {
                System.Diagnostics.Debug.WriteLine(v.SectionName + " " + v.Relocations.Count);
                for (int i = 0; i < v.Relocations.Count; i++)
                {
                    if (!AllSymbols.Contains(v.Relocations[i].Symbol) && !linkFiles.ContainsSymbol(CppSanatize(v.Relocations[i].Symbol.Symbol)))
                        throw new Exception("Missing Symbol " + v.Relocations[i].Symbol.Symbol + " " + v.Symbol);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static string CppSanatize(string s)
        {
            if (s.StartsWith("_Z"))
            {
                int length = 0;
                int i;
                for (i = 2; i < s.Length; i++)
                {
                    if (s[i] < '0' || s[i] > '9')
                        break;
                    length *= 10;
                    length += s[i] - '0';
                }

                if (length <= 0)
                    return s;

                return s.Substring(i, length);
            }
            return s;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public HSDAccessor GenerateFunctionDAT(LinkFile link, string[] functions, bool debug, bool quiet = false)
        {
            // Generate Function DAT
            var function = new HSDAccessor() { _s = new HSDStruct(0x20) };

            // Generate code section
            HSDStruct debug_symbol_table = null;
            int debug_symbol_count = 0;
            Dictionary<SymbolData, long> dataToOffset = new Dictionary<SymbolData, long>();
            byte[] codedata;
            using (MemoryStream code = new MemoryStream())
            {
                // create debug symbol table
                if (debug)
                    debug_symbol_table = new HSDStruct((AllSymbols.Count + 1) * 0xC);

                // process all code
                foreach (var v in AllSymbols)
                {
                    // align
                    if (code.Length % 4 != 0)
                        code.Write(new byte[4 - (code.Length % 4)], 0, 4 - ((int)code.Length % 4));

                    int code_start = (int)code.Position;
                    // write code
                    if (v.Data.Length == 0 && link.TryGetSymbolAddress(CppSanatize(v.Symbol), out uint addr))
                    {
                        dataToOffset.Add(v, addr);
                    }
                    else
                    {
                        dataToOffset.Add(v, code.Length);
                        code.Write(v.Data, 0, v.Data.Length);
                    }
                    int code_end = (int)code.Position;

                    //Console.WriteLine($"{v.SectionName} {v.Symbol} Start: {code_start.ToString("X")} End: {code_end.ToString("X")} ");

                    if (debug && code_start != code_end)
                    {
                        debug_symbol_table.SetInt32(debug_symbol_count * 0xC, code_start);
                        debug_symbol_table.SetInt32(debug_symbol_count * 0xC + 4, code_end);
                        debug_symbol_table.SetString(debug_symbol_count * 0xC + 8, string.IsNullOrEmpty(v.Symbol) ? v.SectionName : v.Symbol, true);
                        debug_symbol_count++;
                    }
                }
                codedata = code.ToArray();

                // resize debug table
                if (debug)
                    debug_symbol_table.Resize(debug_symbol_count * 0xC);
            }

            // generate function table
            HSDStruct functionTable = new HSDStruct(8);
            var funcCount = 0;
            var fl = functions.ToList();
            foreach (var v in SymbolToData)
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
            foreach (var v in AllSymbols)
            {
                // check data length
                if (v.Data.Length == 0)
                    if (link.ContainsSymbol(CppSanatize(v.Symbol)))
                        continue;
                    else
                        throw new Exception($"Error: {v.Symbol} length is {v.Data.Length.ToString("X")}");

                // print debug info
                if (!quiet)
                {
                    Console.WriteLine($"{v.Symbol,-30} {v.SectionName,-50} Offset: {dataToOffset[v].ToString("X8"),-16} Length: {v.Data.Length.ToString("X8")}");

                    if (v.Relocations.Count > 0)
                        Console.WriteLine($"\t {"Section:",-50} {"RelocType:",-20} {"FuncOffset:",-16} {"SectionOffset:"}");
                }

                // process and create relocation table
                foreach (var reloc in v.Relocations)
                {
                    if (!quiet)
                        Console.WriteLine($"\t {reloc.Symbol.SectionName,-50} {reloc.Type,-20} {reloc.Offset.ToString("X8"),-16} {reloc.AddEnd.ToString("X8")}");

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
                        case (RelocType)0x6D:
                            break;
                        default:
                            // no exception, but not guarenteed to work
                            Console.WriteLine($"Warning: unsupported reloc type {toFunctionOffset.ToString("X")} " + reloc.Type.ToString("X") + $" in {v.Symbol} to {reloc.Symbol.Symbol} send this to Ploaj or UnclePunch");
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
                            case (RelocType)0x6D:
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
                    if (addEntry)
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="lelf"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        public HSDRawFile BuildDatFile(IEnumerable<string> symbols = null)
        {
            Dictionary<SymbolData, HSDAccessor> symbolToAccessor = new Dictionary<SymbolData, HSDAccessor>();

            if (symbols == null)
                symbols = AllSymbols.Select(e => e.Symbol);

            // convert to accessors
            foreach (var d in AllSymbols)
            {
                symbolToAccessor.Add(d, new HSDAccessor() { _s = new HSDStruct(d.Data) });
            }

            // check relocations
            foreach (var d in AllSymbols)
            {
                foreach (var r in d.Relocations)
                {
                    if (r.Type == RelocType.R_PPC_ADDR32)
                    {
                        symbolToAccessor[d]._s.SetReference((int)r.Offset, symbolToAccessor[r.Symbol]);
                    }
                    else
                    {
                        // TODO relocation error checking
                        //Console.WriteLine($"DAT Files don't support relocation type {r.Type}");
                        //return null;
                    }
                }
            }

            // generate dat file
            HSDRawFile file = new HSDRawFile();

            foreach (var v in symbols)
            {
                var data = AllSymbols.Find(e => e.Symbol.Equals(v));

                Console.WriteLine("Found " + v + ": " + !(data == null));

                if (data != null)
                    file.Roots.Add(new HSDRootNode()
                    {
                        Name = v,
                        Data = symbolToAccessor[data]
                    });
            }

            return file;
        }
    }
}
