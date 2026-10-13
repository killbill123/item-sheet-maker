using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ItemSheetMaker
{
    public class MainForm : Form
    {
        PhotoLibrary lib = new PhotoLibrary(null);
        readonly List<string> items = new List<string>();

        TextBox txtFolder, txtCode, txtTitle;
        Label lblFolderInfo, lblCount, lblPage;
        ListBox lstItems;
        RadioButton rbAuto, rbFixed;
        NumericUpDown numCols, numRows, numFont;
        CheckBox chkTrim;
        PictureBox preview;
        Button btnPrev, btnNext;
        int pageIndex = 0;
        Timer refreshTimer;

        static string SettingsPath
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ItemSheetMaker");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.txt");
            }
        }

        public MainForm()
        {
            Text = "Item Sheet Maker";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Width = 1200; Height = 820;
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;

            var left = new Panel { Dock = DockStyle.Left, Width = 360, AutoScroll = true, Padding = new Padding(10) };
            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(225, 228, 232) };
            Controls.Add(right);
            Controls.Add(left);

            int y = 10;
            Func<string, int, Label> header = (t, yy) =>
            {
                var l = new Label { Text = t, Left = 10, Top = yy, AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
                left.Controls.Add(l); return l;
            };

            header("1. Photos folder", y); y += 26;
            txtFolder = new TextBox { Left = 10, Top = y, Width = 250, ReadOnly = true };
            var btnBrowse = new Button { Text = "Browse...", Left = 265, Top = y - 1, Width = 75 };
            btnBrowse.Click += (s, e) => BrowseFolder();
            left.Controls.Add(txtFolder); left.Controls.Add(btnBrowse); y += 28;
            lblFolderInfo = new Label { Left = 10, Top = y, Width = 330, Height = 32, ForeColor = Color.DimGray,
                Text = "Photo file names must be the item code, e.g. WOP-187A.jpg" };
            left.Controls.Add(lblFolderInfo); y += 40;

            header("2. Item codes", y); y += 26;
            txtCode = new TextBox { Left = 10, Top = y, Width = 250, CharacterCasing = CharacterCasing.Upper,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.CustomSource };
            var btnAdd = new Button { Text = "Add", Left = 265, Top = y - 1, Width = 75 };
            btnAdd.Click += (s, e) => AddCodes(txtCode.Text);
            txtCode.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { AddCodes(txtCode.Text); e.Handled = true; e.SuppressKeyPress = true; }
            };
            left.Controls.Add(txtCode); left.Controls.Add(btnAdd); y += 26;
            left.Controls.Add(new Label { Left = 10, Top = y, Width = 330, Height = 30, ForeColor = Color.DimGray,
                Text = "Type a code and press Enter. You can also paste many codes separated by commas, spaces or new lines." });
            y += 34;

            lstItems = new ListBox { Left = 10, Top = y, Width = 250, Height = 220, SelectionMode = SelectionMode.MultiExtended,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 20 };
            lstItems.DrawItem += DrawListItem;
            lstItems.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
            left.Controls.Add(lstItems);
            var btnUp = new Button { Text = "Up", Left = 265, Top = y, Width = 75 };
            var btnDown = new Button { Text = "Down", Left = 265, Top = y + 30, Width = 75 };
            var btnRemove = new Button { Text = "Remove", Left = 265, Top = y + 70, Width = 75 };
            var btnClear = new Button { Text = "Clear all", Left = 265, Top = y + 100, Width = 75 };
            btnUp.Click += (s, e) => MoveItem(-1);
            btnDown.Click += (s, e) => MoveItem(1);
            btnRemove.Click += (s, e) => RemoveSelected();
            btnClear.Click += (s, e) => { if (items.Count > 0 && Confirm("Remove all items?")) { items.Clear(); RefreshAll(); } };
            left.Controls.AddRange(new Control[] { btnUp, btnDown, btnRemove, btnClear });
            y += 224;
            lblCount = new Label { Left = 10, Top = y, Width = 330, Height = 20 };
            left.Controls.Add(lblCount); y += 28;

            header("3. Sheet layout", y); y += 26;
            left.Controls.Add(new Label { Text = "Title (optional):", Left = 10, Top = y + 3, AutoSize = true });
            txtTitle = new TextBox { Left = 110, Top = y, Width = 230 };
            txtTitle.TextChanged += (s, e) => QueueRefresh();
            left.Controls.Add(txtTitle); y += 30;

            rbAuto = new RadioButton { Text = "Auto-fit all items on one page", Left = 10, Top = y, Width = 330, Checked = true };
            left.Controls.Add(rbAuto); y += 24;
            rbFixed = new RadioButton { Text = "Fixed grid:", Left = 10, Top = y, Width = 90 };
            numCols = new NumericUpDown { Left = 105, Top = y, Width = 50, Minimum = 1, Maximum = 8, Value = 4 };
            left.Controls.Add(new Label { Text = "columns  x", Left = 158, Top = y + 3, AutoSize = true });
            numRows = new NumericUpDown { Left = 225, Top = y, Width = 50, Minimum = 1, Maximum = 10, Value = 2 };
            left.Controls.Add(new Label { Text = "rows", Left = 278, Top = y + 3, AutoSize = true });
            left.Controls.AddRange(new Control[] { rbFixed, numCols, numRows }); y += 30;

            left.Controls.Add(new Label { Text = "Code text size (pt):", Left = 10, Top = y + 3, AutoSize = true });
            numFont = new NumericUpDown { Left = 130, Top = y, Width = 55, Minimum = 6, Maximum = 36, Value = 12 };
            left.Controls.Add(numFont); y += 30;
            chkTrim = new CheckBox { Text = "Trim white space around photos", Left = 10, Top = y, Width = 330, Checked = true };
            left.Controls.Add(chkTrim); y += 34;

            foreach (Control c in new Control[] { rbAuto, rbFixed, numCols, numRows, numFont, chkTrim })
            {
                if (c is RadioButton) ((RadioButton)c).CheckedChanged += (s, e) => { UpdateEnabled(); QueueRefresh(); };
                else if (c is CheckBox) ((CheckBox)c).CheckedChanged += (s, e) => QueueRefresh();
                else ((NumericUpDown)c).ValueChanged += (s, e) => { if (c != numFont) rbFixed.Checked = true; QueueRefresh(); };
            }

            header("4. Save", y); y += 28;
            var btnPdf = new Button { Text = "Save as PDF", Left = 10, Top = y, Width = 105, Height = 34, BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var btnJpg = new Button { Text = "Save as JPEG", Left = 122, Top = y, Width = 105, Height = 34 };
            var btnPrint = new Button { Text = "Print...", Left = 234, Top = y, Width = 105, Height = 34 };
            btnPdf.Click += (s, e) => SavePdf();
            btnJpg.Click += (s, e) => SaveJpeg();
            btnPrint.Click += (s, e) => Print();
            left.Controls.AddRange(new Control[] { btnPdf, btnJpg, btnPrint });
            y += 44;

            // preview side
            var nav = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(245, 246, 248) };
            btnPrev = new Button { Text = "< Prev", Left = 10, Top = 5, Width = 70 };
            btnNext = new Button { Text = "Next >", Left = 200, Top = 5, Width = 70 };
            lblPage = new Label { Left = 85, Top = 10, Width = 110, TextAlign = ContentAlignment.TopCenter, Text = "Page 1 of 1" };
            btnPrev.Click += (s, e) => { pageIndex--; RefreshPreview(); };
            btnNext.Click += (s, e) => { pageIndex++; RefreshPreview(); };
            nav.Controls.AddRange(new Control[] { btnPrev, lblPage, btnNext });
            nav.Controls.Add(new Label { Left = 290, Top = 10, AutoSize = true, ForeColor = Color.DimGray, Text = "Preview — Letter size 8.5 x 11 in" });
            preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(225, 228, 232), Padding = new Padding(12) };
            right.Controls.Add(preview);
            right.Controls.Add(nav);

            refreshTimer = new Timer { Interval = 250 };
            refreshTimer.Tick += (s, e) => { refreshTimer.Stop(); RefreshPreview(); };

            LoadSettings();
            UpdateEnabled();
            RefreshAll();
            Shown += (s, e) => txtCode.Focus();
        }

        // ---------- folder ----------
        void BrowseFolder()
        {
            using (var d = new FolderBrowserDialog { Description = "Choose the folder that contains your product photos (file name = item code)" })
            {
                if (Directory.Exists(txtFolder.Text)) d.SelectedPath = txtFolder.Text;
                if (d.ShowDialog(this) == DialogResult.OK) SetFolder(d.SelectedPath);
            }
        }

        void SetFolder(string folder)
        {
            Cursor = Cursors.WaitCursor;
            lib.Dispose();
            lib = new PhotoLibrary(folder);
            txtFolder.Text = folder ?? "";
            var ac = new AutoCompleteStringCollection();
            ac.AddRange(lib.Codes.Select(c => c.ToUpperInvariant()).ToArray());
            txtCode.AutoCompleteCustomSource = ac;
            lblFolderInfo.Text = string.IsNullOrEmpty(folder) ? "Photo file names must be the item code, e.g. WOP-187A.jpg"
                : lib.Count + " photos found. File names must be the item code, e.g. WOP-187A.jpg";
            Cursor = Cursors.Default;
            SaveSettings();
            RefreshAll();
        }

        // ---------- items ----------
        void AddCodes(string text)
        {
            var codes = (text ?? "").Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim().ToUpperInvariant()).Where(c => c.Length > 0).ToList();
            if (codes.Count == 0) return;
            var missing = new List<string>();
            foreach (var c in codes)
            {
                items.Add(c);
                if (lib.Find(c) == null) missing.Add(c);
            }
            txtCode.Clear();
            txtCode.Focus();
            RefreshAll();
            lstItems.ClearSelected();
            lstItems.TopIndex = Math.Max(0, items.Count - 1);
            if (missing.Count > 0 && lib.Count > 0)
                System.Media.SystemSounds.Exclamation.Play();
        }

        void RemoveSelected()
        {
            var idx = lstItems.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
            foreach (var i in idx) items.RemoveAt(i);
            RefreshAll();
        }

        void MoveItem(int dir)
        {
            if (lstItems.SelectedIndices.Count != 1) return;
            int i = lstItems.SelectedIndex, j = i + dir;
            if (j < 0 || j >= items.Count) return;
            var t = items[i]; items[i] = items[j]; items[j] = t;
            RefreshAll();
            lstItems.ClearSelected();
            lstItems.SelectedIndex = j;
        }

        void DrawListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= items.Count) return;
            e.DrawBackground();
            var code = items[e.Index];
            bool found = lib.Find(code) != null;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            var color = sel ? SystemColors.HighlightText : (found ? Color.Black : Color.Firebrick);
            string text = (e.Index + 1) + ".  " + code + (found ? "" : "   (no photo found)");
            TextRenderer.DrawText(e.Graphics, text, lstItems.Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height),
                color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            e.DrawFocusRectangle();
        }

        // ---------- rendering ----------
        SheetOptions Options()
        {
            return new SheetOptions
            {
                AutoFit = rbAuto.Checked,
                Columns = (int)numCols.Value,
                Rows = (int)numRows.Value,
                CodeFontPt = (float)numFont.Value,
                TrimWhite = chkTrim.Checked,
                Title = txtTitle.Text
            };
        }

        void UpdateEnabled()
        {
            // keep the numeric boxes enabled so changing them switches to fixed grid
        }

        void RefreshAll()
        {
            lstItems.BeginUpdate();
            lstItems.Items.Clear();
            foreach (var c in items) lstItems.Items.Add(c);
            lstItems.EndUpdate();
            int missing = items.Count(c => lib.Find(c) == null);
            lblCount.Text = items.Count + " item(s)" + (missing > 0 ? "  —  " + missing + " without photo" : "");
            lblCount.ForeColor = missing > 0 ? Color.Firebrick : Color.Black;
            RefreshPreview();
        }

        void QueueRefresh() { refreshTimer.Stop(); refreshTimer.Start(); }

        void RefreshPreview()
        {
            int cols, rows;
            var o = Options();
            var pages = SheetRenderer.Paginate(items, o, out cols, out rows);
            pageIndex = Math.Max(0, Math.Min(pageIndex, pages.Count - 1));
            lblPage.Text = "Page " + (pageIndex + 1) + " of " + pages.Count;
            btnPrev.Enabled = pageIndex > 0;
            btnNext.Enabled = pageIndex < pages.Count - 1;
            if (o.AutoFit && items.Count > 0) { numCols.Tag = 1; }
            var old = preview.Image;
            preview.Image = SheetRenderer.RenderPage(pages[pageIndex], cols, rows, o, lib, 110f, pageIndex + 1, pages.Count);
            if (old != null) old.Dispose();
        }

        List<Bitmap> RenderAll(float dpi)
        {
            int cols, rows;
            var o = Options();
            var pages = SheetRenderer.Paginate(items, o, out cols, out rows);
            var list = new List<Bitmap>();
            for (int i = 0; i < pages.Count; i++)
                list.Add(SheetRenderer.RenderPage(pages[i], cols, rows, o, lib, dpi, i + 1, pages.Count));
            return list;
        }

        bool ReadyToSave()
        {
            if (items.Count == 0) { MessageBox.Show(this, "Add at least one item code first.", Text); return false; }
            var missing = items.Where(c => lib.Find(c) == null).Distinct().ToList();
            if (missing.Count > 0)
                return Confirm("No photo found for: " + string.Join(", ", missing.Take(15)) + (missing.Count > 15 ? "..." : "") +
                    "\n\nThese will show an empty box. Continue anyway?");
            return true;
        }

        string DefaultName()
        {
            var t = string.IsNullOrWhiteSpace(txtTitle.Text) ? "Item Sheet " + DateTime.Now.ToString("yyyy-MM-dd") : txtTitle.Text.Trim();
            foreach (var ch in Path.GetInvalidFileNameChars()) t = t.Replace(ch, '-');
            return t;
        }

        void SavePdf()
        {
            if (!ReadyToSave()) return;
            using (var d = new SaveFileDialog { Filter = "PDF file (*.pdf)|*.pdf", FileName = DefaultName() + ".pdf" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                Cursor = Cursors.WaitCursor;
                try
                {
                    var bmps = RenderAll(300f);
                    var jpgs = bmps.Select(b => SheetRenderer.ToJpeg(b, 92)).ToList();
                    PdfWriter.Write(d.FileName, jpgs, bmps[0].Width, bmps[0].Height);
                    foreach (var b in bmps) b.Dispose();
                    Cursor = Cursors.Default;
                    if (Confirm("Saved:\n" + d.FileName + "\n\nOpen it now?")) OpenFile(d.FileName);
                }
                catch (Exception ex) { Cursor = Cursors.Default; MessageBox.Show(this, "Could not save PDF:\n" + ex.Message, Text); }
            }
        }

        void SaveJpeg()
        {
            if (!ReadyToSave()) return;
            using (var d = new SaveFileDialog { Filter = "JPEG image (*.jpg)|*.jpg", FileName = DefaultName() + ".jpg" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                Cursor = Cursors.WaitCursor;
                try
                {
                    var bmps = RenderAll(300f);
                    var saved = new List<string>();
                    string dir = Path.GetDirectoryName(d.FileName), stem = Path.GetFileNameWithoutExtension(d.FileName);
                    for (int i = 0; i < bmps.Count; i++)
                    {
                        string p = bmps.Count == 1 ? d.FileName : Path.Combine(dir, stem + " - page " + (i + 1) + ".jpg");
                        File.WriteAllBytes(p, SheetRenderer.ToJpeg(bmps[i], 92));
                        saved.Add(p);
                        bmps[i].Dispose();
                    }
                    Cursor = Cursors.Default;
                    if (Confirm("Saved " + saved.Count + " JPEG file(s):\n" + string.Join("\n", saved.Select(Path.GetFileName)) + "\n\nOpen the first one now?"))
                        OpenFile(saved[0]);
                }
                catch (Exception ex) { Cursor = Cursors.Default; MessageBox.Show(this, "Could not save JPEG:\n" + ex.Message, Text); }
            }
        }

        void Print()
        {
            if (!ReadyToSave()) return;
            var bmps = RenderAll(300f);
            int idx = 0;
            using (var doc = new PrintDocument())
            {
                doc.DocumentName = DefaultName();
                foreach (PaperSize ps in doc.PrinterSettings.PaperSizes)
                    if (ps.Kind == PaperKind.Letter) { doc.DefaultPageSettings.PaperSize = ps; break; }
                doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                doc.PrintPage += (s, e) =>
                {
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
                    e.Graphics.DrawImage(bmps[idx], new Rectangle(0, 0, 850, 1100)); // hundredths of an inch
                    idx++;
                    e.HasMorePages = idx < bmps.Count;
                };
                using (var pd = new PrintDialog { Document = doc, UseEXDialog = true })
                {
                    if (pd.ShowDialog(this) == DialogResult.OK)
                    {
                        try { doc.Print(); } catch (Exception ex) { MessageBox.Show(this, "Print failed:\n" + ex.Message, Text); }
                    }
                }
            }
            foreach (var b in bmps) b.Dispose();
        }

        // ---------- helpers ----------
        bool Confirm(string msg) { return MessageBox.Show(this, msg, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; }

        static void OpenFile(string p)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(p) { UseShellExecute = true }); } catch { }
        }

        void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    int i = line.IndexOf('=');
                    if (i < 0) continue;
                    string k = line.Substring(0, i), v = line.Substring(i + 1);
                    if (k == "folder" && Directory.Exists(v)) SetFolder(v);
                }
            }
            catch { }
        }

        void SaveSettings()
        {
            try { File.WriteAllText(SettingsPath, "folder=" + txtFolder.Text + "\n"); } catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveSettings();
            lib.Dispose();
            base.OnFormClosed(e);
        }
    }
}
