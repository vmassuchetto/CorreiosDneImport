using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DneImport
{
    public class FileMappingComparer : IComparer<FileInfo>
    {
        /// <summary>
        /// Comparar arquivos segundo ordem devida de importação
        /// </summary>
        /// <param name="f1">Descritor do arquivo 1</param>
        /// <param name="f2">Descritor do arquivo 2</param>
        /// <returns></returns>
        public int Compare(FileInfo f1, FileInfo f2)
        {
            int i1 = Array.IndexOf(DneImport.Mapping.Keys.ToArray(), DneImport.GetTableFromFileName(f1.Name));
            int i2 = Array.IndexOf(DneImport.Mapping.Keys.ToArray(), DneImport.GetTableFromFileName(f2.Name));
            if (i1 > i2)
                return 1;
            else if (i1 < i2)
                return -1;
            else
                return 0;
        }
    }
}