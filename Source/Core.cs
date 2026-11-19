// 核心：配置 / 图片裁切 / 排版尺寸 / 页序列构建 / docx 打包
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace PaperToWord
{
    enum SplitMode { Auto = 0, LR = 1, TB = 2, None = 3 }

    sealed class Config
    {
        public SplitMode Mode = SplitMode.Auto;    // 裁切方式
        public double Position = 50.0;             // 裁切线位置 %
        public int Quality = 300;                  // 目标 DPI；0 = 不降采样（原图）
        public bool TwoUp = false;                 // true：左右两片上下排在同一页
        public double FillRatio = 0.96;            // 单片占版心比例
        public int MaxSourceSide = 4600;           // 输入长边降采样上限；0 = 不降
        public bool AutoLandscape = true;          // 不裁切的横图：该页自动改用横向 A4 撑满
    }

    sealed class Slice
    {
        public int W, H;
        public byte[] Jpeg;
    }

    // 一页（一个分节）内的图片
    sealed class Page
    {
        public readonly List<string> RelIds = new List<string>();
        public readonly List<long> EmuW = new List<long>();
        public readonly List<long> EmuH = new List<long>();
        public readonly List<string> Names = new List<string>();
        public bool Landscape;   // true：该页用横向 A4（不裁切的横图自动撑满）
    }

    static class Metrics
    {
        public const long MmToEmu = 36000L;
        public const long PageWmm = 210, PageHmm = 297, MarginMm = 5;

        public static readonly long TextWEmu = (PageWmm - 2 * MarginMm) * MmToEmu;  // 200mm
        public static readonly long TextHEmu = (PageHmm - 2 * MarginMm) * MmToEmu;  // 287mm

        // 横向 A4：宽高互换
        public static readonly long TextWLandscapeEmu = TextHEmu;   // 287mm
        public static readonly long TextHLandscapeEmu = TextWEmu;   // 200mm

        const long StackGapMm = 3;
        // 两片共一页时，每片的可用高度
        public static long HalfStackEmu { get { return (TextHEmu - StackGapMm * MmToEmu) / 2; } }
    }

    static class ImageUtil
    {
        public static readonly string[] AllowedExts = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

        public static string FilterText()
        {
            return "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|JPEG|*.jpg;*.jpeg|PNG|*.png|所有文件|*.*";
        }

        public static bool IsAllowed(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            ext = ext.ToLowerInvariant();
            for (int i = 0; i < AllowedExts.Length; i++)
                if (ext == AllowedExts[i]) return true;
            return false;
        }

        // 读图（内存流，不锁文件），并修正 EXIF 方向
        public static Image Load(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            MemoryStream ms = new MemoryStream(bytes);
            Image img;
            try { img = Image.FromStream(ms, false, true); }
            catch { ms.Dispose(); throw new InvalidDataException("不是可识别的图片文件"); }
            // GDI+ 要求流与 Image 同生命周期：挂到 Tag 上防止被 GC 提前回收
            img.Tag = ms;
            try { TryFixExifOrientation(img); } catch { }
            if (img.Width < 8 || img.Height < 8)
            {
                img.Dispose();
                throw new InvalidDataException("图片尺寸过小");
            }
            return img;
        }

        static void TryFixExifOrientation(Image img)
        {
            PropertyItem pi;
            try { pi = img.GetPropertyItem(0x0112); } catch { return; }
            if (pi == null || pi.Value == null || pi.Value.Length < 1) return;
            int o = pi.Value[0];
            if (o <= 1 || o > 8) return;
            RotateFlipType r = RotateFlipType.RotateNoneFlipNone;
            switch (o)
            {
                case 2: r = RotateFlipType.RotateNoneFlipX; break;
                case 3: r = RotateFlipType.Rotate180FlipNone; break;
                case 4: r = RotateFlipType.Rotate180FlipX; break;
                case 5: r = RotateFlipType.Rotate90FlipX; break;
                case 6: r = RotateFlipType.Rotate90FlipNone; break;
                case 7: r = RotateFlipType.Rotate270FlipX; break;
                case 8: r = RotateFlipType.Rotate270FlipNone; break;
            }
            img.RotateFlip(r);
        }

        public static Image DownscaleIfNeeded(Image img, int maxSide)
        {
            int W = img.Width, H = img.Height;
            if (maxSide <= 0 || Math.Max(W, H) <= maxSide) return img;
            double s = (double)maxSide / Math.Max(W, H);
            int nw = Math.Max(1, (int)Math.Round(W * s));
            int nh = Math.Max(1, (int)Math.Round(H * s));
            Bitmap outBmp = new Bitmap(nw, nh, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(outBmp))
            {
                g.Clear(Color.White);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(img, new Rectangle(0, 0, nw, nh), new Rectangle(0, 0, W, H), GraphicsUnit.Pixel);
            }
            return outBmp;
        }

        static ImageCodecInfo _codec;
        static ImageCodecInfo JpegCodec()
        {
            if (_codec == null)
            {
                try
                {
                    foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders())
                        if (string.Equals(c.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase)) { _codec = c; break; }
                }
                catch { }
            }
            return _codec;
        }

        public static byte[] ToJpeg(Bitmap bmp, int quality)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ImageCodecInfo codec = JpegCodec();
                if (codec != null)
                {
                    using (EncoderParameters ep = new EncoderParameters(1))
                    {
                        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                        bmp.Save(ms, codec, ep);
                    }
                }
                else bmp.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }

        // 从 src 裁出 rect，按目标 DPI 缩放到版心可容纳的像素，编码 JPEG
        public static Slice MakeSlice(Image src, Rectangle rect, int dpi, bool halfStack, bool landscape, int jpegQuality)
        {
            double s = 1.0;
            if (dpi > 0)
            {
                double boxWmm = (landscape ? Metrics.TextWLandscapeEmu : Metrics.TextWEmu) / (double)Metrics.MmToEmu;
                double boxHmm = (halfStack ? Metrics.HalfStackEmu : (landscape ? Metrics.TextHLandscapeEmu : Metrics.TextHEmu)) / (double)Metrics.MmToEmu;
                double boxWpx = boxWmm / 25.4 * dpi;
                double boxHpx = boxHmm / 25.4 * dpi;
                s = Math.Min(boxWpx / rect.Width, boxHpx / rect.Height);
                if (s > 1.0) s = 1.0;
            }
            int tw = Math.Max(1, (int)Math.Round(rect.Width * s));
            int th = Math.Max(1, (int)Math.Round(rect.Height * s));

            using (Bitmap bmp = new Bitmap(tw, th, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.DrawImage(src, new Rectangle(0, 0, tw, th), rect, GraphicsUnit.Pixel);
                }
                byte[] jpg = ToJpeg(bmp, jpegQuality);
                Slice sl = new Slice();
                sl.W = tw; sl.H = th; sl.Jpeg = jpg;
                return sl;
            }
        }
    }

    static class Cutter
    {
        // 计算裁片矩形
        public static List<Rectangle> Plan(int W, int H, SplitMode mode, double pct)
        {
            List<Rectangle> list = new List<Rectangle>();
            bool cut = true, vertical = true;
            if (mode == SplitMode.None) cut = false;
            else if (mode == SplitMode.Auto) { cut = W > H; vertical = true; }
            else if (mode == SplitMode.LR) { cut = true; vertical = true; }
            else { cut = true; vertical = false; }

            if (!cut)
            {
                list.Add(new Rectangle(0, 0, W, H));
                return list;
            }
            double p = Math.Max(20.0, Math.Min(80.0, pct)) / 100.0;
            if (vertical)
            {
                int x = Math.Max(1, Math.Min(W - 1, (int)Math.Round(W * p)));
                list.Add(new Rectangle(0, 0, x, H));
                list.Add(new Rectangle(x, 0, W - x, H));
            }
            else
            {
                int y = Math.Max(1, Math.Min(H - 1, (int)Math.Round(H * p)));
                list.Add(new Rectangle(0, 0, W, y));
                list.Add(new Rectangle(0, y, W, H - y));
            }
            return list;
        }

        public static bool IsVertical(List<Rectangle> cuts)
        {
            return cuts.Count == 2 && cuts[0].Top == cuts[1].Top;
        }

        // 等比缩放进 maxW x maxH 的 EMU 盒
        public static void FitEmu(int pxW, int pxH, long maxW, long maxH, double ratio, out long w, out long h)
        {
            long tW = (long)(maxW * ratio), tH = (long)(maxH * ratio);
            double s = Math.Min((double)tW / pxW, (double)tH / pxH);
            w = Math.Max(1, (long)(pxW * s));
            h = Math.Max(1, (long)(pxH * s));
            if (w > tW) { w = tW; h = Math.Max(1, (long)((double)w * pxH / pxW)); }
            if (h > tH) { h = tH; w = Math.Max(1, (long)((double)h * pxW / pxH)); }
        }
    }

    static class Sizer
    {
        public static List<Page> Build(string[] files, Config cfg, Action<string> log, out List<string> errors)
        {
            List<Page> pages = new List<Page>();
            errors = new List<string>();
            int dpi = cfg.Quality > 0 ? cfg.Quality : 0;
            int jpegQ = dpi <= 0 ? 92 : (dpi >= 300 ? 88 : 85);
            int nextImage = 1;

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string name = Path.GetFileName(file);
                try
                {
                    using (Image full = ImageUtil.Load(file))
                    {
                        Image src = ImageUtil.DownscaleIfNeeded(full, cfg.MaxSourceSide);
                        bool owned = !ReferenceEquals(src, full);
                        try
                        {
                            List<Rectangle> rects = Cutter.Plan(src.Width, src.Height, cfg.Mode, cfg.Position);
                            // 单片（未裁切）且是横图：整页改用横向 A4，避免上下大片空白
                            bool landscape = rects.Count == 1 && src.Width > src.Height && cfg.AutoLandscape;
                            List<Slice> slices = new List<Slice>();
                            for (int r = 0; r < rects.Count; r++)
                                slices.Add(ImageUtil.MakeSlice(src, rects[r], dpi, cfg.TwoUp, landscape, jpegQ));

                            if (cfg.TwoUp && slices.Count == 2)
                            {
                                Page pg = new Page();
                                AddPiece(pg, slices[0], "左半页 · " + name, cfg, true, false, ref nextImage);
                                AddPiece(pg, slices[1], "右半页 · " + name, cfg, true, false, ref nextImage);
                                pages.Add(pg);
                                if (log != null) log(name + "  →  1 页（上下两片）");
                            }
                            else
                            {
                                for (int s = 0; s < slices.Count; s++)
                                {
                                    Page pg = new Page();
                                    pg.Landscape = landscape;
                                    string tag = slices.Count == 1 ? name
                                        : name + " · " + (Cutter.IsVertical(rects) ? (s == 0 ? "左半页" : "右半页") : (s == 0 ? "上半页" : "下半页"));
                                    AddPiece(pg, slices[s], tag, cfg, false, landscape, ref nextImage);
                                    pages.Add(pg);
                                }
                                if (log != null) log(name + "  →  " + slices.Count + (landscape ? " 页（横向撑满）" : " 页"));
                            }
                        }
                        finally { if (owned) src.Dispose(); }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(name + "：" + ex.Message);
                    if (log != null) log("跳过 " + name + "（" + ex.Message + "）");
                }
            }
            return pages;
        }

        static void AddPiece(Page pg, Slice sl, string name, Config cfg, bool twoUp, bool landscape, ref int nextImage)
        {
            string rid = "rId" + nextImage.ToString(CultureInfo.InvariantCulture);
            string target = "media/image" + nextImage.ToString(CultureInfo.InvariantCulture) + ".jpg";
            nextImage++;

            long maxW = landscape ? Metrics.TextWLandscapeEmu : Metrics.TextWEmu;
            long maxH = twoUp ? Metrics.HalfStackEmu : (landscape ? Metrics.TextHLandscapeEmu : Metrics.TextHEmu);
            double ratio = twoUp ? 0.98 : cfg.FillRatio;
            long w, h;
            Cutter.FitEmu(sl.W, sl.H, maxW, maxH, ratio, out w, out h);

            pg.RelIds.Add(rid);
            pg.EmuW.Add(w);
            pg.EmuH.Add(h);
            pg.Names.Add(name);
            MediaStore.Put(target, sl.Jpeg);
        }
    }

    // 构建期媒体暂存，写包时取出
    static class MediaStore
    {
        static readonly Dictionary<string, byte[]> Map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        public static void Put(string target, byte[] data) { Map[target] = data; }
        public static IEnumerable<KeyValuePair<string, byte[]>> All { get { return Map; } }
        public static void Clear() { Map.Clear(); }
        public static int Count { get { return Map.Count; } }
        public static long TotalBytes
        {
            get { long t = 0; foreach (KeyValuePair<string, byte[]> kv in Map) t += kv.Value.LongLength; return t; }
        }
    }

    // docx 输出（临时文件 + 原子改名，目标被占用时自动换名）
    static class DocxBuilder
    {
        public static string Write(string desiredPath, List<Page> pages)
        {
            string path = PickFreePath(desiredPath);
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string tmp = Path.Combine(dir, ".esw_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");
            try
            {
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipWriter zip = new ZipWriter(fs))
                {
                    zip.Add("[Content_Types].xml", Ooxml.Utf8(Ooxml.ContentTypes()));
                    zip.Add("_rels/.rels", Ooxml.Utf8(Ooxml.RootRels()));
                    zip.Add("docProps/core.xml", Ooxml.Utf8(Ooxml.Core()));
                    zip.Add("docProps/app.xml", Ooxml.Utf8(Ooxml.App()));
                    zip.Add("word/styles.xml", Ooxml.Utf8(Ooxml.Styles()));
                    zip.Add("word/settings.xml", Ooxml.Utf8(Ooxml.Settings()));
                    zip.Add("word/document.xml", Ooxml.Utf8(Ooxml.Document(pages)));
                    zip.Add("word/_rels/document.xml.rels", Ooxml.Utf8(Ooxml.DocRels(pages)));
                    foreach (KeyValuePair<string, byte[]> kv in MediaStore.All)
                        zip.Add("word/" + kv.Key, kv.Value);
                    fs.Flush();
                }
                if (File.Exists(full)) File.Delete(full);
                File.Move(tmp, full);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
            return full;
        }

        static string PickFreePath(string desired)
        {
            string full;
            try { full = Path.GetFullPath(desired); } catch { full = desired; }
            if (!File.Exists(full)) return full;
            string dir = Path.GetDirectoryName(full);
            string stem = Path.GetFileNameWithoutExtension(full);
            string ext = Path.GetExtension(full);
            for (int i = 2; i < 200; i++)
            {
                string cand = Path.Combine(dir, stem + "_" + i.ToString(CultureInfo.InvariantCulture) + ext);
                if (!File.Exists(cand)) return cand;
            }
            return Path.Combine(dir, stem + "_" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ext);
        }
    }
}
