// 自然排序：文件名中的数字段按数值比较（“第2页”排在“第10页”前面）
using System;
using System.Collections.Generic;
using System.IO;

namespace PaperToWord
{
    static class NatString
    {
        public static int CmpPath(string a, string b)
        {
            int r = Compare(SafeName(a), SafeName(b));
            if (r != 0) return r;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        static string SafeName(string path)
        {
            try { return Path.GetFileName(path); } catch { return path; }
        }

        public static int Compare(string s1, string s2)
        {
            if (s1 == null) s1 = "";
            if (s2 == null) s2 = "";
            int i = 0, j = 0;
            while (i < s1.Length && j < s2.Length)
            {
                char c1 = s1[i], c2 = s2[j];
                if (char.IsDigit(c1) && char.IsDigit(c2))
                {
                    int si = i, sj = j;
                    while (i < s1.Length && char.IsDigit(s1[i])) i++;
                    while (j < s2.Length && char.IsDigit(s2[j])) j++;
                    string n1 = s1.Substring(si, i - si).TrimStart('0');
                    string n2 = s2.Substring(sj, j - sj).TrimStart('0');
                    if (n1.Length != n2.Length) return n1.Length - n2.Length;
                    int cmp = string.CompareOrdinal(n1, n2);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    int cmp = char.ToLowerInvariant(c1).CompareTo(char.ToLowerInvariant(c2));
                    if (cmp != 0) return cmp;
                    i++; j++;
                }
            }
            return (s1.Length - i) - (s2.Length - j);
        }
    }
}
