using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DneImport
{
    public class FileNameComparer : IComparer<string>
    {
        /// <summary>
        /// Retira a fração de data do nome de um arquivo eDNE_Master_(0000).zip
        /// </summary>
        /// <param name="fileName">Arquivo</param>
        /// <returns>Data do arquivo</returns>
        public static int GetDateFromFileName(String fileName)
        {
            var regex = new Regex(@"eDNE_.*_(?<Date>[0-9]{4}).zip");
            var result = regex.Match(fileName);
            if (result.Success)
                return Int32.Parse(result.Groups["Date"].Value);
            return 0;
        }

        /// <summary>
        /// Comparar dois nomes de arquivo para mantê-los na ordem de importação
        /// </summary>
        /// <param name="f1">Nome de arquivo 1</param>
        /// <param name="f2">Nome de arquivo 2</param>
        /// <returns></returns>
        public int Compare(string f1, string f2)
        {
            var d1 = GetDateFromFileName(f1);
            var d2 = GetDateFromFileName(f2);

            if (d1 > d2)
                return 1;
            else if (d1 < d2)
                return -1;
            if (d1 == d2 && f1.Contains("Delta_Master"))
                return 1;
            else if (d1 == d2 && f2.Contains("Delta_Master"))
                return -1;
            else
                return 0;
        }
    }
}