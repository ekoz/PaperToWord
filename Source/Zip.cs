// 极简 ZIP 写入器（store 存储，docx/Office 完全兼容；不依赖 System.IO.Compression）
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PaperToWord
{
    sealed class ZipWriter : IDisposable
    {
        readonly Stream _s;
        readonly List<Entry> _entries = new List<Entry>();
        readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool _done;

        struct Entry
        {
            public string Name;
            public uint Crc;
            public uint Size;
            public uint Offset;
        }

        public ZipWriter(Stream output) { _s = output; }

        public void Add(string name, byte[] data)
        {
            if (_done) throw new InvalidOperationException("zip 已关闭");
            name = name.Replace('\\', '/');
            if (!_names.Add(name)) throw new InvalidOperationException("重复条目 " + name);
            byte[] nb = Encoding.UTF8.GetBytes(name);
            uint crc = Crc32(data);

            uint headerOffset = (uint)_s.Position;   // 中央目录要指向 local header 起始

            WriteU32(0x04034B50);      // local file header
            WriteU16(20);              // version needed
            WriteU16(0x0800);          // flags: UTF-8 names
            WriteU16(0);               // method = store
            DateTime now = DateTime.Now;
            WriteU16(TimeField(now));
            WriteU16(DateField(now));
            WriteU32(crc);
            WriteU32((uint)data.Length);
            WriteU32((uint)data.Length);
            WriteU16((ushort)nb.Length);
            WriteU16(0);
            _s.Write(nb, 0, nb.Length);
            _s.Write(data, 0, data.Length);

            _entries.Add(new Entry
            {
                Name = name,
                Crc = crc,
                Size = (uint)data.Length,
                Offset = headerOffset
            });
        }

        public void Finish()
        {
            if (_done) return;
            _done = true;
            uint cdStart = (uint)_s.Position;
            uint cdSize = 0;
            DateTime now = DateTime.Now;
            ushort t = TimeField(now), d = DateField(now);
            foreach (Entry e in _entries)
            {
                byte[] nb = Encoding.UTF8.GetBytes(e.Name);
                WriteU32(0x02014B50);
                WriteU16(20); WriteU16(20);
                WriteU16(0x0800); WriteU16(0);
                WriteU16(t); WriteU16(d);
                WriteU32(e.Crc); WriteU32(e.Size); WriteU32(e.Size);
                WriteU16((ushort)nb.Length); WriteU16(0); WriteU16(0);
                WriteU16(0); WriteU16(0);
                WriteU32(0); WriteU32(e.Offset);
                _s.Write(nb, 0, nb.Length);
                cdSize += (uint)(46 + nb.Length);
            }
            WriteU32(0x06054B50);
            WriteU16(0); WriteU16(0);
            WriteU16((ushort)_entries.Count); WriteU16((ushort)_entries.Count);
            WriteU32(cdSize); WriteU32(cdStart);
            WriteU16(0);
            _s.Flush();
        }

        static ushort TimeField(DateTime n) { return (ushort)((n.Hour << 11) | (n.Minute << 5) | (n.Second / 2)); }
        static ushort DateField(DateTime n) { return (ushort)(((Math.Max(1980, n.Year) - 1980) << 9) | (n.Month << 5) | n.Day); }

        void WriteU16(int v) { _s.WriteByte((byte)(v & 0xFF)); _s.WriteByte((byte)((v >> 8) & 0xFF)); }
        void WriteU32(uint v)
        {
            _s.WriteByte((byte)(v & 0xFF));
            _s.WriteByte((byte)((v >> 8) & 0xFF));
            _s.WriteByte((byte)((v >> 16) & 0xFF));
            _s.WriteByte((byte)((v >> 24) & 0xFF));
        }

        static readonly uint[] Table = BuildTable();
        static uint[] BuildTable()
        {
            uint[] t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = ((c & 1) != 0) ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                t[i] = c;
            }
            return t;
        }
        public static uint Crc32(byte[] data)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++) c = Table[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        public void Dispose() { Finish(); }
    }
}
