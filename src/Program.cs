using System.Collections.Generic;
using System;
using System.Linq;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Item Sheet Maker")]
[assembly: System.Reflection.AssemblyProduct("Item Sheet Maker")]
[assembly: System.Reflection.AssemblyVersion("1.6.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.6.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.6")]

namespace ItemSheetMaker
{
    static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            // Command-line: ItemSheetMaker.exe --render <photoFolder> <out.pdf|out.jpg> CODE1 CODE2=24 ...
            if (args.Length >= 3 && args[0] == "--render")
            {
                var lib = new PhotoLibrary(args[1]);
                var raw = args.Skip(3).ToList();
                var codes = raw.Select(a => a.Split('=')[0]).ToList();
                var qtys = raw.Select(a => a.Contains("=") ? a.Substring(a.IndexOf('=') + 1) : "").ToList();
                var o = new SheetOptions { Quantities = qtys };
                int c, r;
                var pages = SheetRenderer.Paginate(codes, o, out c, out r);
                var starts = new List<int>(); int acc = 0;
                foreach (var pg in pages) { starts.Add(acc); acc += pg.Count; }
                var bmps = pages.Select((p, i) => SheetRenderer.RenderPage(p, c, r, o, lib, 300f, i + 1, pages.Count, starts[i])).ToList();
                if (args[2].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    PdfWriter.Write(args[2], bmps.Select(b => SheetRenderer.ToJpeg(b, 92)).ToList(), bmps[0].Width, bmps[0].Height);
                else
                    System.IO.File.WriteAllBytes(args[2], SheetRenderer.ToJpeg(bmps[0], 92));
                return;
            }
            try { if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
