using System;
using System.IO;

namespace MexTK.Tools
{
    public class FileTools
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool CheckFileExists(string input)
        {
            if (!File.Exists(input) || (Path.GetExtension(input) != ".c" && Path.GetExtension(input) != ".o"))
            {
                Console.WriteLine($"Error:\n{input} does not exist or is invalid\nPress enter to exit");
                Console.ReadLine();
                return false;
            }
            return true;
        }
    }
}
