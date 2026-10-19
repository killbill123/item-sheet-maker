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
        readonly Dictionary<string, string> manualPhotos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> items = new List<string>();

        TextBox txtFolder, txtCode, txtTitle;
        Label lblFolderInfo, lblCount, lblPage;
        ListBox lstItems;
        RadioButton rbAuto, rbFixed;
        NumericUpDown numCols, numRows, numFont;
        CheckBox chkTrim;
        PagePreview preview;
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

        // Illustrator-style dark workspace palette
        public static readonly Color Chrome = Color.FromArgb(0x26, 0x26, 0x26);   // app bar, borders
        public static readonly Color Panel = Color.FromArgb(0x32, 0x32, 0x32);    // panels
        public static readonly Color PanelAlt = Color.FromArgb(0x2C, 0x2C, 0x2C);
        public static readonly Color Canvas = Color.FromArgb(0x53, 0x53, 0x53);   // pasteboard
        public static readonly Color Field = Color.FromArgb(0x24, 0x24, 0x24);
        public static readonly Color Hover = Color.FromArgb(0x45, 0x45, 0x45);
        public static readonly Color Text1 = Color.FromArgb(0xE6, 0xE6, 0xE6);
        public static readonly Color Text2 = Color.FromArgb(0xA8, 0xA8, 0xA8);
        public static readonly Color Line = Color.FromArgb(0x1E, 0x1E, 0x1E);
        public static readonly Color Blue = Color.FromArgb(0x14, 0x73, 0xE6);     // primary action
        public static readonly Color BlueDark = Color.FromArgb(0x0D, 0x66, 0xD0);
        public static readonly Color Select = Color.FromArgb(0x2B, 0x4A, 0x73);
        public static readonly Color Warn = Color.FromArgb(0xF2, 0x9A, 0x2E);

        static Button Btn(string text, bool primary)
        {
            var b = new Button
            {
                Text = text, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Height = 28,
                BackColor = primary ? Blue : Color.FromArgb(0x3A, 0x3A, 0x3A), ForeColor = primary ? Color.White : Text1,
                Font = new Font("Segoe UI", 9f, primary ? FontStyle.Bold : FontStyle.Regular), UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(0x50, 0x50, 0x50);
            b.FlatAppearance.MouseOverBackColor = primary ? BlueDark : Hover;
            b.FlatAppearance.MouseDownBackColor = primary ? BlueDark : Hover;
            return b;
        }

        static TextBox DarkText(int x, int y, int w)
        {
            return new TextBox { Left = x, Top = y, Width = w, BorderStyle = BorderStyle.FixedSingle, BackColor = Field, ForeColor = Text1 };
        }

        static NumericUpDown DarkNum(int x, int y, int w, int min, int max, int val)
        {
            return new NumericUpDown { Left = x, Top = y, Width = w, Minimum = min, Maximum = max, Value = val,
                BorderStyle = BorderStyle.FixedSingle, BackColor = Field, ForeColor = Text1 };
        }

        DocTab docTab;
        Label lblStatus;

        public MainForm()
        {
            Text = "Item Sheet Maker";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Width = 1280; Height = 860;
            MinimumSize = new Size(980, 640);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9f);
            BackColor = Panel;
            ForeColor = Text1;
            KeyPreview = true;

            // ----- frame: app bar, tool strip, properties panel, status bar, canvas -----
            var appBar = new AppBar { Dock = DockStyle.Top, Height = 40 };
            var status = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Chrome };
            var tools = new ToolRail { Dock = DockStyle.Left, Width = 46 };
            var props = new Panel { Dock = DockStyle.Right, Width = 330, AutoScroll = true, BackColor = Panel };
            props.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Line });
            var center = new Panel { Dock = DockStyle.Fill, BackColor = Canvas };
            Controls.Add(center);
            Controls.Add(props);
            Controls.Add(tools);
            Controls.Add(status);
            Controls.Add(appBar);

            docTab = new DocTab { Dock = DockStyle.Top, Height = 30 };
            preview = new PagePreview { Dock = DockStyle.Fill, BackColor = Canvas };
            center.Controls.Add(preview);
            center.Controls.Add(docTab);

            // ----- properties panel -----
            int y = 0;
            Func<string, int, PanelHeader> section = (t, yy) =>
            {
                var h = new PanelHeader { Text = t, Left = 1, Top = yy, Width = 312, Height = 30 };
                props.Controls.Add(h); return h;
            };
            Func<string, int, int, int, Label> hint = (t, xx, yy, h) =>
            {
                var l = new Label { Text = t, Left = xx, Top = yy, Width = 290, Height = h, ForeColor = Text2, Font = new Font("Segoe UI", 8.25f) };
                props.Controls.Add(l); return l;
            };
            Func<string, int, int, Label> lbl = (t, xx, yy) =>
            {
                var l = new Label { Text = t, Left = xx, Top = yy, AutoSize = true, ForeColor = Text2 };
                props.Controls.Add(l); return l;
            };

            section("Photos", y); y += 38;
            txtFolder = DarkText(14, y + 2, 206); txtFolder.ReadOnly = true;
            var btnBrowse = Btn("Browse...", false); btnBrowse.Left = 226; btnBrowse.Top = y; btnBrowse.Width = 80;
            btnBrowse.Click += (s, e) => BrowseFolder();
            props.Controls.Add(txtFolder); props.Controls.Add(btnBrowse); y += 34;
            lblFolderInfo = hint("Photo file names must be the item code, e.g. WOP-187A.jpg", 14, y, 30); y += 38;

            section("Items", y); y += 38;
            txtCode = DarkText(14, y + 2, 206);
            txtCode.CharacterCasing = CharacterCasing.Upper; txtCode.Font = new Font("Segoe UI", 10f);
            txtCode.AutoCompleteMode = AutoCompleteMode.SuggestAppend; txtCode.AutoCompleteSource = AutoCompleteSource.CustomSource;
            var btnAdd = Btn("Add", true); btnAdd.Left = 226; btnAdd.Top = y; btnAdd.Width = 80;
            btnAdd.Click += (s, e) => AddCodes(txtCode.Text);
            txtCode.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { AddCodes(txtCode.Text); e.Handled = true; e.SuppressKeyPress = true; }
            };
            props.Controls.Add(txtCode); props.Controls.Add(btnAdd); y += 34;
            hint("Press Enter after each code or paste a list. Drag photos in, or double-click a missing item to pick its photo.", 14, y, 30); y += 36;

            lstItems = new ListBox { Left = 14, Top = y, Width = 292, Height = 162, SelectionMode = SelectionMode.MultiExtended,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 24, BorderStyle = BorderStyle.FixedSingle, BackColor = PanelAlt,
                ForeColor = Text1, IntegralHeight = false };
            lstItems.DrawItem += DrawListItem;
            lstItems.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); else if (e.KeyCode == Keys.Enter) ChoosePhotoForSelected(); };
            lstItems.DoubleClick += (s, e) => ChoosePhotoForSelected();
            props.Controls.Add(lstItems); y += 168;
            var btnUp = Btn("Up", false); var btnDown = Btn("Down", false);
            var btnRemove = Btn("Remove", false); var btnClear = Btn("Clear", false);
            int bx = 14;
            foreach (var b in new[] { btnUp, btnDown, btnRemove, btnClear }) { b.Left = bx; b.Top = y; b.Width = 69; bx += 74; }
            btnUp.Click += (s, e) => MoveItem(-1);
            btnDown.Click += (s, e) => MoveItem(1);
            btnRemove.Click += (s, e) => RemoveSelected();
            btnClear.Click += (s, e) => { if (items.Count > 0 && Confirm("Remove all items?")) { items.Clear(); RefreshAll(); } };
            props.Controls.AddRange(new Control[] { btnUp, btnDown, btnRemove, btnClear });
            y += 34;
            lblCount = new Label { Left = 14, Top = y, Width = 292, Height = 20, Font = new Font("Segoe UI", 8.25f) };
            props.Controls.Add(lblCount); y += 28;

            section("Artboard", y); y += 38;
            lbl("Sheet heading", 14, y);
            y += 20;
            txtTitle = DarkText(14, y, 292);
            txtTitle.HandleCreated += (s, e) => SetCueBanner(txtTitle, "e.g. client name (printed at top)");
            txtTitle.TextChanged += (s, e) => { QueueRefresh(); UpdateDocTab(); };
            props.Controls.Add(txtTitle); y += 34;

            rbAuto = new RadioButton { Text = "Auto-fit all items on one page", Left = 14, Top = y, Width = 290, Checked = true, ForeColor = Text1 };
            props.Controls.Add(rbAuto); y += 26;
            rbFixed = new RadioButton { Text = "Grid", Left = 14, Top = y, Width = 60, ForeColor = Text1 };
            numCols = DarkNum(80, y, 50, 1, 8, 4);
            lbl("cols  x", 134, y + 3);
            numRows = DarkNum(184, y, 50, 1, 10, 2);
            lbl("rows", 238, y + 3);
            props.Controls.AddRange(new Control[] { rbFixed, numCols, numRows }); y += 32;

            lbl("Code size", 14, y + 3);
            numFont = DarkNum(80, y, 50, 6, 36, 12);
            lbl("pt", 134, y + 3);
            props.Controls.Add(numFont); y += 30;
            chkTrim = new CheckBox { Text = "Trim white space around photos", Left = 14, Top = y, Width = 290, Checked = true, ForeColor = Text1 };
            props.Controls.Add(chkTrim); y += 34;

            foreach (Control c in new Control[] { rbAuto, rbFixed, numCols, numRows, numFont, chkTrim })
            {
                if (c is RadioButton) ((RadioButton)c).CheckedChanged += (s, e) => { UpdateEnabled(); QueueRefresh(); };
                else if (c is CheckBox) ((CheckBox)c).CheckedChanged += (s, e) => QueueRefresh();
                else ((NumericUpDown)c).ValueChanged += (s, e) => { if (c != numFont) rbFixed.Checked = true; QueueRefresh(); };
            }

            section("Export", y); y += 40;
            var btnPdf = Btn("Export PDF", true);
            var btnJpg = Btn("Export JPEG", false);
            var btnPrint = Btn("Print...", false);
            btnPdf.Left = 14; btnJpg.Left = 112; btnPrint.Left = 210;
            foreach (var b in new[] { btnPdf, btnJpg, btnPrint }) { b.Top = y; b.Width = 94; b.Height = 32; }
            btnPdf.Click += (s, e) => SavePdf();
            btnJpg.Click += (s, e) => SaveJpeg();
            btnPrint.Click += (s, e) => Print();
            props.Controls.AddRange(new Control[] { btnPdf, btnJpg, btnPrint });
            y += 50;
            props.Controls.Add(new Panel { Left = 0, Top = y, Width = 10, Height = 1, BackColor = Panel });

            // ----- tool rail -----
            tools.Add("Photos folder (Ctrl+O)", ToolRail.Icon.Folder, () => BrowseFolder());
            tools.Add("Add item code (Ctrl+N)", ToolRail.Icon.Plus, () => { txtCode.Focus(); txtCode.SelectAll(); });
            tools.Add("Toggle auto-fit / grid (Ctrl+G)", ToolRail.Icon.Grid, () => { if (rbAuto.Checked) rbFixed.Checked = true; else rbAuto.Checked = true; });
            tools.AddSeparator();
            tools.Add("Export PDF (Ctrl+E)", ToolRail.Icon.Pdf, () => SavePdf());
            tools.Add("Export JPEG (Ctrl+J)", ToolRail.Icon.Image, () => SaveJpeg());
            tools.Add("Print (Ctrl+P)", ToolRail.Icon.Printer, () => Print());
            tools.AddSeparator();
            tools.Add("Check for updates", ToolRail.Icon.Update, () => Updater.CheckAsync(this, true));
            KeyDown += (s, e) =>
            {
                if (!e.Control) return;
                if (e.KeyCode == Keys.O) BrowseFolder();
                else if (e.KeyCode == Keys.N) txtCode.Focus();
                else if (e.KeyCode == Keys.G) { if (rbAuto.Checked) rbFixed.Checked = true; else rbAuto.Checked = true; }
                else if (e.KeyCode == Keys.E) SavePdf();
                else if (e.KeyCode == Keys.J) SaveJpeg();
                else if (e.KeyCode == Keys.P) Print();
                else return;
                e.Handled = true; e.SuppressKeyPress = true;
            };

            // ----- status bar: artboard navigation -----
            btnPrev = Btn("<", false); btnPrev.Left = 8; btnPrev.Top = 3; btnPrev.Width = 26; btnPrev.Height = 20;
            lblPage = new Label { Left = 36, Top = 5, Width = 92, TextAlign = ContentAlignment.TopCenter, Text = "Page 1 of 1", ForeColor = Text1 };
            btnNext = Btn(">", false); btnNext.Left = 130; btnNext.Top = 3; btnNext.Width = 26; btnNext.Height = 20;
            btnPrev.Font = btnNext.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            btnPrev.Click += (s, e) => { pageIndex--; RefreshPreview(); };
            btnNext.Click += (s, e) => { pageIndex++; RefreshPreview(); };
            lblStatus = new Label { Left = 170, Top = 5, Width = 700, ForeColor = Text2, Text = "" };
            status.Controls.AddRange(new Control[] { btnPrev, lblPage, btnNext, lblStatus });
            UpdateDocTab();

            refreshTimer = new Timer { Interval = 250 };
            refreshTimer.Tick += (s, e) => { refreshTimer.Stop(); RefreshPreview(); };

            LoadSettings();
            UpdateEnabled();
            RefreshAll();
            foreach (Control c in new Control[] { this, preview, lstItems, props, center, docTab, tools, txtCode })
                EnableDrop(c);
            Shown += (s, e) =>
            {
                txtCode.Focus();
                var t = new Timer { Interval = 2500 };
                t.Tick += (s2, e2) => { t.Stop(); t.Dispose(); Updater.CheckAsync(this, false); };
                t.Start();
            };
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
            foreach (var kv in manualPhotos) lib.SetOverride(kv.Key, kv.Value);
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

        // ---------- drag and drop / manual photos ----------
        void EnableDrop(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += (s, e) =>
            {
                e.Effect = DroppedPaths(e.Data).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
                preview.DropActive = e.Effect != DragDropEffects.None;
            };
            c.DragOver += (s, e) => { e.Effect = DroppedPaths(e.Data).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None; };
            c.DragLeave += (s, e) => { preview.DropActive = false; };
            c.DragDrop += (s, e) =>
            {
                preview.DropActive = false;
                int row = -1;
                if (s == lstItems)
                {
                    var pt = lstItems.PointToClient(new Point(e.X, e.Y));
                    row = lstItems.IndexFromPoint(pt);
                }
                HandleDrop(DroppedPaths(e.Data), row);
            };
        }

        static string[] DroppedPaths(IDataObject data)
        {
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop)) return new string[0];
            var paths = data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null) return new string[0];
            return paths.Where(p => Directory.Exists(p) || (File.Exists(p) && PhotoLibrary.IsImageFile(p))).ToArray();
        }

        /// <summary>
        /// Folder dropped: use it as the photos folder.
        /// One photo dropped on a list row: use it for that item.
        /// Photos dropped anywhere else: each fills a missing item with the same code, or is added as a new item named after the file.
        /// </summary>
        void HandleDrop(string[] paths, int row)
        {
            if (paths.Length == 0) return;
            if (paths.Length == 1 && Directory.Exists(paths[0])) { SetFolder(paths[0]); return; }
            var files = paths.Where(File.Exists).ToList();
            if (files.Count == 0) return;

            if (files.Count == 1 && row >= 0 && row < items.Count)
            {
                AssignPhoto(items[row], files[0]);
                RefreshAll();
                return;
            }
            int added = 0, filled = 0;
            foreach (var f in files)
            {
                var code = Path.GetFileNameWithoutExtension(f).Trim().ToUpperInvariant();
                if (code.Length == 0) continue;
                if (items.Any(i => string.Equals(i, code, StringComparison.OrdinalIgnoreCase))) filled++;
                else { items.Add(code); added++; }
                AssignPhoto(code, f);
            }
            RefreshAll();
            lstItems.ClearSelected();
            lstItems.TopIndex = Math.Max(0, items.Count - 1);
        }

        void AssignPhoto(string code, string path)
        {
            manualPhotos[code.Trim()] = path;
            lib.SetOverride(code, path);
        }

        void ChoosePhotoForSelected()
        {
            if (lstItems.SelectedIndices.Count != 1) return;
            var code = items[lstItems.SelectedIndex];
            using (var d = new OpenFileDialog
            {
                Title = "Choose photo for " + code,
                Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|All files (*.*)|*.*"
            })
            {
                if (Directory.Exists(txtFolder.Text)) d.InitialDirectory = txtFolder.Text;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                int idx = lstItems.SelectedIndex;
                AssignPhoto(code, d.FileName);
                RefreshAll();
                lstItems.SelectedIndex = idx;
            }
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
            var g = e.Graphics;
            var code = items[e.Index];
            bool found = lib.Find(code) != null;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(sel ? Select : (e.Index % 2 == 0 ? PanelAlt : Color.FromArgb(0x30, 0x30, 0x30))))
                g.FillRectangle(bg, e.Bounds);
            if (sel) using (var bar = new SolidBrush(Blue)) g.FillRectangle(bar, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
            // tiny thumbnail swatch like a layer row
            var sw = new Rectangle(e.Bounds.X + 32, e.Bounds.Y + 2, 20, 20);
            using (var b = new SolidBrush(found ? Color.White : Color.FromArgb(0x55, 0x40, 0x20))) g.FillRectangle(b, sw);
            var th = found ? lib.Load(code, chkTrim.Checked) : null;
            if (th != null)
            {
                float sc = Math.Min(18f / th.Width, 18f / th.Height);
                float tw = th.Width * sc, thh = th.Height * sc;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(th, sw.X + 1 + (18 - tw) / 2, sw.Y + 1 + (18 - thh) / 2, tw, thh);
            }
            using (var pen = new Pen(Color.FromArgb(0x60, 0x60, 0x60))) g.DrawRectangle(pen, sw);
            TextRenderer.DrawText(g, (e.Index + 1).ToString(), lstItems.Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, 26, e.Bounds.Height),
                Text2, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            TextRenderer.DrawText(g, code, lstItems.Font, new Rectangle(e.Bounds.X + 60, e.Bounds.Y, e.Bounds.Width - 60, e.Bounds.Height),
                found ? Text1 : Warn, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            if (found && lib.IsOverride(code))
                TextRenderer.DrawText(g, "added photo", lstItems.Font, new Rectangle(e.Bounds.Right - 90, e.Bounds.Y, 84, e.Bounds.Height),
                    Text2, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            if (!found)
                TextRenderer.DrawText(g, "missing", lstItems.Font, new Rectangle(e.Bounds.Right - 70, e.Bounds.Y, 64, e.Bounds.Height),
                    Warn, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Light grey example text shown while the box is empty (Windows EM_SETCUEBANNER).</summary>
        static void SetCueBanner(TextBox box, string text)
        {
            try { SendMessage(box.Handle, 0x1501, (IntPtr)1, text); } catch { }
        }

        void UpdateArtboardLabel()
        {
            if (preview == null || txtTitle == null) return;
            var name = string.IsNullOrWhiteSpace(txtTitle.Text) ? "Artboard " + (pageIndex + 1) : txtTitle.Text.Trim();
            preview.Label = (pageIndex + 1).ToString("00") + " - " + name + "  (Letter)";
        }

        void UpdateDocTab()
        {
            if (docTab == null) return;
            var name = string.IsNullOrWhiteSpace(txtTitle.Text) ? "Untitled sheet" : txtTitle.Text.Trim();
            docTab.Text = name + " @ Fit  (Letter 8.5 x 11 in)";
            UpdateArtboardLabel();
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
            lblCount.Text = items.Count + (items.Count == 1 ? " item" : " items") + (missing > 0 ? "   ·   " + missing + " without photo" : "");
            lblCount.ForeColor = missing > 0 ? Warn : Text2;
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
            if (o.AutoFit && items.Count > 0) { numCols.Tag = 1; }
            if (lblStatus != null) lblStatus.Text = items.Count + (items.Count == 1 ? " item" : " items") + "   |   " +
                (o.AutoFit ? "Auto-fit" : cols + " x " + rows + " grid") + "   |   8.5 x 11 in   |   Export 300 DPI   |   " +
                (string.IsNullOrEmpty(lib.Folder) ? "No photos folder" : lib.Count + " photos in folder");
            UpdateArtboardLabel();
            preview.SetPage(SheetRenderer.RenderPage(pages[pageIndex], cols, rows, o, lib, 110f, pageIndex + 1, pages.Count));
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
            Settings.Set("folder", txtFolder.Text);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveSettings();
            lib.Dispose();
            base.OnFormClosed(e);
        }
    }
}
