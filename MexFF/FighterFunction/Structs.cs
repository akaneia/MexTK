namespace MexTK.FighterFunction
{
    public struct ELFRelocA
    {
        public uint r_offset;
        public uint r_info;
        public uint r_addend;
        public uint R_SYM => ((r_info >> 8) & 0xFFFFFF);
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

        public override string ToString()
        {
            return $"{st_name} {st_value} {st_size} {(SymbolBinding)(st_info >> 4)} {(SymbolType)(st_info & 0xF)} {st_other} {st_shndx}";
        }
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

        public override string ToString()
        {
            return $"{sh_type} {sh_flags} {sh_link} {sh_addr} {sh_info}";
        }
    }


    public struct ELF_Debug_Line
    {
        public uint length;
        public ushort version;
        public uint header_length;
        public byte min_instruction_length;
        public byte default_is_stmt;
        public sbyte line_base;
        public byte line_range;
        public byte opcode_base;
        public byte[] std_opcode_lengths;

        public override string ToString()
        {
            return $"Debug Line Version: {version}";
        }
    }
}
