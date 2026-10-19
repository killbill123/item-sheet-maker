using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ItemSheetMaker
{
    public class SheetOptions
    {
        public bool AutoFit = true;      // fit all items on one page (up to MaxAutoPerPage)
        public int Columns = 4;
        public int Rows = 2;
        public float CodeFontPt = 12f;
        public bool TrimWhite = true;
        public string Title = "";
        public float MarginIn = 0.5f;
        public const int MaxAutoPerPage = 30;
    }

    /// <summary>Finds photos in a folder by item code (file name = item code).</summary>
    public class PhotoLibrary
    {
        static readonly string[] Exts = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };
        readonly Dictionary<string, string> exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly List<KeyValuePair<string, string>> all = new List<KeyValuePair<string, string>>();
        readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        public string Folder { get; private set; }

        public PhotoLibrary(string folder)
        {
            Folder = folder;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories); }
            catch { files = Directory.EnumerateFiles(folder); }
            foreach (var f in files)
            {
                if (!Exts.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
                var stem = Path.GetFileNameWithoutExtension(f).Trim();
                all.Add(new KeyValuePair<string, string>(stem, f));
                if (!exact.ContainsKey(stem)) exact[stem] = f;
            }
        }

        public int Count { get { return all.Count; } }
        public IEnumerable<string> Codes { get { return exact.Keys.OrderBy(k => k); } }

        readonly Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Use a specific photo file for a code (drag and drop or "Choose photo").</summary>
        public void SetOverride(string code, string path)
        {
            overrides[code.Trim()] = path;
            foreach (var k in cache.Keys.Where(k => k.StartsWith(code.Trim() + "|", StringComparison.OrdinalIgnoreCase)).ToList())
            { cache[k].Dispose(); cache.Remove(k); }
        }

        public bool IsOverride(string code) { return overrides.ContainsKey((code ?? "").Trim()); }

        public static bool IsImageFile(string path) { return Exts.Contains(Path.GetExtension(path).ToLowerInvariant()); }

        public string Find(string code)
        {
            code = (code ?? "").Trim();
            if (code.Length == 0) return null;
            string p;
            if (overrides.TryGetValue(code, out p) && File.Exists(p)) return p;
            if (exact.TryGetValue(code, out p)) return p;
            // Allow names like "WOP-187A front.jpg", "WOP-187A_1.jpg", "WOP-187A (2).jpg"
            foreach (var kv in all)
            {
                if (kv.Key.Length > code.Length && kv.Key.StartsWith(code, StringComparison.OrdinalIgnoreCase))
                {
                    char c = kv.Key[code.Length];
                    if (c == ' ' || c == '_' || c == '(' || c == '.' || c == '-') return kv.Value;
                }
            }
            return null;
        }

        public Bitmap Load(string code, bool trim)
        {
            string key = code + "|" + trim;
            Bitmap b;
            if (cache.TryGetValue(key, out b)) return b;
            var path = Find(code);
            if (path == null) return null;
            try
            {
                Bitmap src;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                {
                    src = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(src)) g.DrawImage(img, 0, 0, img.Width, img.Height);
                }
                if (trim)
                {
                    var r = ContentBounds(src);
                    if (r.Width > 0 && r.Height > 0 && (r.Width < src.Width || r.Height < src.Height))
                    {
                        var t = src.Clone(r, PixelFormat.Format32bppArgb);
                        src.Dispose();
                        src = t;
                    }
                }
                cache[key] = src;
                return src;
            }
            catch { return null; }
        }

        /// <summary>Bounding box of pixels that are not near-white and not transparent.</summary>
        static Rectangle ContentBounds(Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;
            var buf = new byte[stride * bmp.Height];
            Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            bmp.UnlockBits(data);
            int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < bmp.Height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < bmp.Width; x++)
                {
                    int i = row + x * 4;
                    byte bl = buf[i], gr = buf[i + 1], rd = buf[i + 2], a = buf[i + 3];
                    if (a < 20) continue;
                    if (rd > 238 && gr > 238 && bl > 238) continue;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return rect;
            int pad = Math.Max(2, Math.Max(bmp.Width, bmp.Height) / 100);
            minX = Math.Max(0, minX - pad); minY = Math.Max(0, minY - pad);
            maxX = Math.Min(bmp.Width - 1, maxX + pad); maxY = Math.Min(bmp.Height - 1, maxY + pad);
            return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public void Dispose()
        {
            foreach (var b in cache.Values) b.Dispose();
            cache.Clear();
        }
    }

    public static class SheetRenderer
    {
        public const float PageWIn = 8.5f, PageHIn = 11f;

        /// <summary>Splits codes into pages and picks the grid for each page.</summary>
        public static List<List<string>> Paginate(IList<string> codes, SheetOptions o, out int cols, out int rows)
        {
            var pages = new List<List<string>>();
            int perPage;
            if (o.AutoFit)
            {
                int n = Math.Max(1, Math.Min(codes.Count, SheetOptions.MaxAutoPerPage));
                BestGrid(n, o, out cols, out rows);
                perPage = SheetOptions.MaxAutoPerPage < codes.Count ? cols * rows : Math.Max(1, codes.Count);
                perPage = Math.Max(perPage, 1);
            }
            else
            {
                cols = Math.Max(1, o.Columns); rows = Math.Max(1, o.Rows);
                perPage = cols * rows;
            }
            for (int i = 0; i < codes.Count; i += perPage)
                pages.Add(codes.Skip(i).Take(perPage).ToList());
            if (pages.Count == 0) pages.Add(new List<string>());
            return pages;
        }

        static void BestGrid(int n, SheetOptions o, out int bestC, out int bestR)
        {
            float w = PageWIn - 2 * o.MarginIn;
            float h = PageHIn - 2 * o.MarginIn - (string.IsNullOrWhiteSpace(o.Title) ? 0 : 0.6f);
            bestC = 1; bestR = n; float bestS = -1;
            // Never make photos bigger than a 4 x 2 grid, so 1-8 items keep the same size
            if (n <= 8) { bestC = 4; bestR = 2; return; }
            for (int c = 1; c <= Math.Min(n, 6); c++)
            {
                int r = (int)Math.Ceiling(n / (double)c);
                float cw = w / c, ch = h / r - 0.35f; // room for the code label
                float s = Math.Min(cw, ch);
                if (s > bestS + 0.001f) { bestS = s; bestC = c; bestR = r; }
            }
        }

        public static Bitmap RenderPage(List<string> codes, int cols, int rows, SheetOptions o, PhotoLibrary lib,
                                        float dpi, int pageNo, int pageCount)
        {
            int W = (int)Math.Round(PageWIn * dpi), H = (int)Math.Round(PageHIn * dpi);
            var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
            bmp.SetResolution(dpi, dpi);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.PageUnit = GraphicsUnit.Pixel;

                float m = o.MarginIn * dpi;
                float top = m;
                float pxPerPt = dpi / 72f;

                if (!string.IsNullOrWhiteSpace(o.Title))
                {
                    using (var tf = new Font("Arial", 18f * pxPerPt, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(o.Title.Trim(), tf, Brushes.Black, new RectangleF(m, top, W - 2 * m, 0.45f * dpi), sf);
                    }
                    top += 0.6f * dpi;
                }

                float footer = pageCount > 1 ? 0.3f * dpi : 0;
                float areaW = W - 2 * m, areaH = H - top - m - footer;
                float cellW = areaW / cols, cellH = areaH / rows;

                float fontPx = o.CodeFontPt * pxPerPt;
                using (var font = new Font("Arial", fontPx, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                using (var missFont = new Font("Arial", fontPx * 0.8f, FontStyle.Italic, GraphicsUnit.Pixel))
                using (var missPen = new Pen(Color.FromArgb(200, 200, 200), Math.Max(1f, dpi / 100f)) { DashStyle = DashStyle.Dash })
                {
                    float labelH = fontPx * 1.8f;
                    // Don't let cells get much taller than they are wide, otherwise wide photos
                    // leave a big empty gap between rows. Rows are packed together instead.
                    cellH = Math.Min(cellH, cellW * 1.2f + labelH);
                    float pad = Math.Min(cellW, cellH) * 0.06f;
                    int usedRows = Math.Max(1, (int)Math.Ceiling(codes.Count / (double)cols));
                    float yOff = Math.Max(0f, (areaH - Math.Min(rows, usedRows) * cellH) / 2f);   // center the block vertically
                    for (int i = 0; i < codes.Count; i++)
                    {
                        int c = i % cols, r = i / cols;
                        int inRow = Math.Min(cols, codes.Count - r * cols);   // center a partly filled row
                        float x = m + (cols - inRow) * cellW / 2f + c * cellW, y = top + yOff + r * cellH;
                        var imgBox = new RectangleF(x + pad, y + pad, cellW - 2 * pad, cellH - 2 * pad - labelH);
                        var img = lib.Load(codes[i], o.TrimWhite);
                        if (img != null)
                        {
                            float s = Math.Min(imgBox.Width / img.Width, imgBox.Height / img.Height);
                            float iw = img.Width * s, ih = img.Height * s;
                            // center horizontally, sit on the bottom so labels line up
                            var dst = new RectangleF(imgBox.X + (imgBox.Width - iw) / 2, imgBox.Bottom - ih, iw, ih);
                            g.DrawImage(img, dst);
                        }
                        else
                        {
                            float side = Math.Min(imgBox.Width, imgBox.Height) * 0.8f;
                            var box = new RectangleF(imgBox.X + (imgBox.Width - side) / 2, imgBox.Bottom - side, side, side);
                            g.DrawRectangle(missPen, box.X, box.Y, box.Width, box.Height);
                            g.DrawString("No photo found", missFont, Brushes.Gray, box, sf);
                        }
                        var lbl = new RectangleF(x, imgBox.Bottom + pad * 0.3f, cellW, labelH);
                        g.DrawString(codes[i].Trim().ToUpperInvariant(), font, Brushes.Black, lbl, sf);
                    }
                }

                if (pageCount > 1)
                {
                    using (var pf = new Font("Arial", 9f * pxPerPt, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                        g.DrawString("Page " + pageNo + " of " + pageCount, pf, Brushes.Gray,
                            new RectangleF(m, H - m - footer, W - 2 * m, footer), sf);
                }
            }
            return bmp;
        }

        public static byte[] ToJpeg(Bitmap bmp, long quality)
        {
            var enc = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
            using (var ep = new EncoderParameters(1))
            using (var ms = new MemoryStream())
            {
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bmp.Save(ms, enc, ep);
                return ms.ToArray();
            }
        }
    }

    /// <summary>Minimal PDF writer: one full-page JPEG per US Letter page.</summary>
    public static class PdfWriter
    {
        public static void Write(string path, List<byte[]> jpegs, int pxW, int pxH)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var offsets = new List<long>();
                Action<string> w = s => { var b = System.Text.Encoding.ASCII.GetBytes(s); fs.Write(b, 0, b.Length); };
                w("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
                int n = jpegs.Count;
                // objects: 1 catalog, 2 pages, then per page: page, content, image
                Func<int, int> pageObj = i => 3 + i * 3;
                offsets.Add(fs.Position); w("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
                offsets.Add(fs.Position);
                var kids = string.Join(" ", Enumerable.Range(0, n).Select(i => pageObj(i) + " 0 R"));
                w("2 0 obj\n<< /Type /Pages /Kids [" + kids + "] /Count " + n + " >>\nendobj\n");
                for (int i = 0; i < n; i++)
                {
                    int p = pageObj(i), c = p + 1, im = p + 2;
                    offsets.Add(fs.Position);
                    w(p + " 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im0 " + im +
                      " 0 R >> >> /Contents " + c + " 0 R >>\nendobj\n");
                    string content = "q 612 0 0 792 0 0 cm /Im0 Do Q\n";
                    offsets.Add(fs.Position);
                    w(c + " 0 obj\n<< /Length " + content.Length + " >>\nstream\n" + content + "endstream\nendobj\n");
                    offsets.Add(fs.Position);
                    w(im + " 0 obj\n<< /Type /XObject /Subtype /Image /Width " + pxW + " /Height " + pxH +
                      " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpegs[i].Length + " >>\nstream\n");
                    fs.Write(jpegs[i], 0, jpegs[i].Length);
                    w("\nendstream\nendobj\n");
                }
                long xref = fs.Position;
                w("xref\n0 " + (offsets.Count + 1) + "\n0000000000 65535 f \n");
                foreach (var o in offsets) w(o.ToString("D10") + " 00000 n \n");
                w("trailer\n<< /Size " + (offsets.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
            }
        }
    }
}
