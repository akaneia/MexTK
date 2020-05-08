using HSDRaw;

namespace MexTK.Tools
{
    public class DatTools
    {
        /// <summary>
        /// Injects a given symbol into a dat
        /// If symbol already exists it is overwritten
        /// </summary>
        /// <param name="f"></param>
        /// <param name="symbolName"></param>
        /// <param name="function"></param>
        public static void InjectSymbolIntoDat(HSDRawFile f, string symbolName, HSDAccessor function)
        {
            // generate root
            var root = new HSDRootNode();
            root.Name = symbolName;
            root.Data = function;

            // if this symbol already exists in file, replace it
            foreach (var ro in f.Roots)
            {
                if (ro.Name.Equals(root.Name))
                {
                    ro.Data = root.Data;
                }
            }

            // if symbol is not in file, then add it
            if (f.Roots.FindIndex(e => e.Name == root.Name) == -1)
                f.Roots.Add(root);
        }
    }
}
