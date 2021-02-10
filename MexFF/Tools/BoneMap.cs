using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MexTK.Tools
{
    public class BoneMap
    {
        private Dictionary<int, string> IndexToName = new Dictionary<int, string>();
        private Dictionary<string, int> NameToIndex = new Dictionary<string, int>();

        public int Count { get => IndexToName.Count; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="IniFile"></param>
        public BoneMap(string IniFile)
        {
            LoadBoneINI(IniFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<string> GetNames()
        {
            return NameToIndex.Keys.ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public string GetName(int index)
        {
            if (IndexToName.ContainsKey(index))
                return IndexToName[index];

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public int GetIndex(string name)
        {
            if (name != null && NameToIndex.ContainsKey(name))
                return NameToIndex[name];

            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private void LoadBoneINI(string filePath)
        {
            var lines = File.ReadAllLines(filePath);

            foreach (var l in lines)
            {
                var a = l.Split('=');
                var index = int.Parse(a[0].Trim().Replace("JOBJ_", ""));
                var name = a[1].Trim();
                if(!IndexToName.ContainsKey(index))
                    IndexToName.Add(index, name);
                if (!NameToIndex.ContainsKey(name))
                    NameToIndex.Add(name, index);
            }
        }
    }
}
