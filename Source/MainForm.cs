// 主界面：拖拽列表 + 裁切参数 + 预览 + 执行
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PaperToWord
{
    sealed class MainForm : Form
    {
        readonly ListBox _list = new ListBox();
        readonly SplitContainer _split = new SplitContainer();
        readonly Panel _previewHost = new Panel();
        readonly Panel _optsPanel = new Panel();
        readonly Panel _bottom = new Panel();
        readonly TextBox _outBox = new TextBox();
        readonly Button _btnOut = new Button();
        readonly Button _btnOpenDir = new Button();
        readonly Button _btnOpenFile = new Button();
        readonly ComboBox _mode = new ComboBox();
        readonly ComboBox _quality = new ComboBox();
        readonly ComboBox _layout = new ComboBox();
        readonly TrackBar _pos = new TrackBar();
        readonly Label _posVal = new Label();
        readonly Button _btnAdd = new Button();
        readonly Button _btnRemove = new Button();
        readonly Button _btnSort = new Button();
        readonly Button _btnUp = new Button();
        readonly Button _btnDown = new Button();
        readonly Button _btnRun = new Button();
        readonly Button _btnClear = new Button();
        readonly RichTextBox _log = new RichTextBox();
        readonly Label _lblPreview = new Label();
        readonly ToolTip _tip = new ToolTip();
        Label _outLabel;
        readonly Label _lblPosHint = new Label();
        readonly Label _lblWork = new Label();
        readonly CheckBox _chkLandscape = new CheckBox();

        readonly List<string> _files = new List<string>();
        volatile bool _busy;
        Image _cachedImg;
        string _cachedFile;

        const string Placeholder = "留空＝自动保存到第一张图所在文件夹";

        public MainForm()
        {
            Text = "PaperToWord  —  原图对半裁开，一键排成 A4 的 Word";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(960, 640);
            Size = new Size(1080, 720);
            Font = new Font("Microsoft YaHei UI", 9F);
            AllowDrop = true;
            try
            {
                Icon appIcon = Program.LoadAppIcon();   // 与 exe 文件图标同一张 crop.png
                if (appIcon != null) Icon = appIcon;
                else Icon = MakeIcon();                  // 兜底：程序自绘
            }
            catch { }

            BuildLayout();
            HookEvents();
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    using (SolidBrush paper = new SolidBrush(Color.White))
                        g.FillRectangle(paper, 3, 2, 12, 28);
                    using (SolidBrush blue = new SolidBrush(Color.FromArgb(43, 87, 154)))
                        g.FillRectangle(blue, 17, 2, 12, 28);
                    using (Pen pen = new Pen(Color.FromArgb(230, 126, 34), 2))
                        g.DrawLine(pen, 16, 0, 16, 31);
                    using (Pen gray = new Pen(Color.FromArgb(120, 120, 120), 1))
                    {
                        g.DrawRectangle(gray, 3, 2, 12, 28);
                        g.DrawRectangle(gray, 17, 2, 12, 28);
                    }
                }
                return (Icon)Icon.FromHandle(bmp.GetHicon()).Clone();
            }
        }

        void BuildLayout()
        {
            // ---- 顶部工具条 ----
            var top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 48;
            ToolButton(_btnAdd, "添加图片", 8, 9, 96);
            ToolButton(_btnRemove, "移除所选", 110, 9, 90);
            ToolButton(_btnClear, "清空", 206, 9, 60);
            ToolButton(_btnSort, "智能排序", 272, 9, 90);
            ToolButton(_btnUp, "上移 ↑", 368, 9, 70);
            ToolButton(_btnDown, "下移 ↓", 444, 9, 70);
            var lblTip = new Label();
            lblTip.Text = "提示：可把图片（或整个文件夹）直接拖进窗口；列表顺序＝试卷页序";
            lblTip.Bounds = new Rectangle(528, 15, 540, 20);
            lblTip.ForeColor = Color.FromArgb(110, 110, 110);
            top.Controls.AddRange(new Control[] { _btnAdd, _btnRemove, _btnClear, _btnSort, _btnUp, _btnDown, lblTip });

            // ---- 底部：输出路径 + 执行 ----
            _bottom.Dock = DockStyle.Bottom;
            _bottom.Height = 96;
            _outLabel = new Label();
            _outLabel.Text = "输出 docx：";
            _outLabel.Bounds = new Rectangle(12, 14, 76, 22);
            _outLabel.TextAlign = ContentAlignment.MiddleLeft;
            _outBox.Bounds = new Rectangle(92, 12, 500, 26);
            SetPlaceholder();
            _btnOut.Text = "浏览…";
            _btnOut.Bounds = new Rectangle(604, 12, 90, 26);
            _btnOpenDir.Text = "打开目录";
            _btnOpenDir.Bounds = new Rectangle(700, 12, 90, 26);
            _btnOpenFile.Text = "打开文件";
            _btnOpenFile.Bounds = new Rectangle(796, 12, 90, 26);
            _btnRun.Text = "▶ 执行：裁开并生成 Word";
            _btnRun.Bounds = new Rectangle(92, 48, 270, 40);
            _btnRun.BackColor = Color.FromArgb(43, 87, 154);
            _btnRun.ForeColor = Color.White;
            _btnRun.FlatStyle = FlatStyle.Flat;
            _btnRun.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            _btnRun.TabStop = false;
            _bottom.Controls.AddRange(new Control[] { _outLabel, _outBox, _btnOut, _btnOpenDir, _btnOpenFile, _btnRun });
            // 科目快捷键：一键填充输出名（今日日期-科目.docx）
            string[,] subjects = { { "数学", "shuxue" }, { "语文", "yuwen" }, { "英语", "en" } };
            int sx = 380;
            for (int i = 0; i < 3; i++)
            {
                Button bs = new Button();
                bs.Text = subjects[i, 0];
                bs.SetBounds(sx + i * 74, 52, 64, 32);
                bs.FlatStyle = FlatStyle.System;
                string key = subjects[i, 1];
                bs.Click += delegate { FillSubjectName(key); };
                _tip.SetToolTip(bs, "输出名填为：今日日期-" + key + ".docx");
                _bottom.Controls.Add(bs);
            }
            _bottom.Resize += delegate
            {
                const int bw = 90, gap = 6;
                int x = Math.Max(92, _bottom.ClientRectangle.Width - 8 - bw);
                _btnOpenFile.Left = x; x -= bw + gap;
                _btnOpenDir.Left = x; x -= bw + gap;
                _btnOut.Left = x;
                _outBox.Width = Math.Max(160, _btnOut.Left - 92 - 8);
            };

            // ---- 中部：左列表 / 右预览 ----
            _split.Dock = DockStyle.Fill;
            _split.SplitterWidth = 6;
            _list.Dock = DockStyle.Fill;
            _list.SelectionMode = SelectionMode.MultiExtended;
            _list.HorizontalScrollbar = true;
            _list.IntegralHeight = false;
            _list.AllowDrop = true;
            _split.Panel1.Controls.Add(_list);

            var right = new Panel();
            right.Dock = DockStyle.Fill;

            var pvWrap = new Panel();
            pvWrap.Dock = DockStyle.Fill;
            _previewHost.Dock = DockStyle.Fill;
            _previewHost.BackColor = Color.FromArgb(236, 238, 242);
            _previewHost.Paint += PreviewHost_Paint;
            _lblPreview.Dock = DockStyle.Top;
            _lblPreview.Height = 24;
            _lblPreview.ForeColor = Color.FromArgb(90, 90, 90);
            _lblPreview.Text = "选中左侧图片可预览裁切效果（红色虚线＝裁切位置）";
            _lblPreview.TextAlign = ContentAlignment.MiddleLeft;
            pvWrap.Controls.Add(_previewHost);
            pvWrap.Controls.Add(_lblPreview);

            BuildOptions();

            _log.Dock = DockStyle.Bottom;
            _log.Height = 110;
            _log.ReadOnly = true;
            _log.BackColor = Color.FromArgb(250, 250, 250);
            _log.Font = new Font("Consolas", 9F);

            right.Controls.Add(pvWrap);
            right.Controls.Add(_optsPanel);
            right.Controls.Add(_log);
            _split.Panel2.Controls.Add(right);

            Controls.Add(_split);
            Controls.Add(_bottom);
            Controls.Add(top);
        }

        static void ToolButton(Button b, string text, int x, int y, int w)
        {
            b.Text = text;
            b.SetBounds(x, y, w, 30);
            b.FlatStyle = FlatStyle.System;
        }

        void BuildOptions()
        {
            _optsPanel.Dock = DockStyle.Top;
            _optsPanel.Height = 132;
            _optsPanel.Padding = new Padding(12, 8, 12, 6);

            var t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 6;
            t.RowCount = 4;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _mode.DropDownStyle = ComboBoxStyle.DropDownList;
            _mode.Items.AddRange(new object[] { "自动：横版裁", "强制左右裁", "强制上下裁", "不裁切" });
            _mode.SelectedIndex = 0;
            _quality.DropDownStyle = ComboBoxStyle.DropDownList;
            _quality.Items.AddRange(new object[] { "150 DPI", "200 DPI", "300 DPI 推荐", "原图(大)" });
            _quality.SelectedIndex = 2;
            _layout.DropDownStyle = ComboBoxStyle.DropDownList;
            _layout.Items.AddRange(new object[] { "每片一页 推荐", "两片同页" });
            _layout.SelectedIndex = 0;

            _pos.Minimum = 30;
            _pos.Maximum = 70;
            _pos.Value = 50;
            _pos.TickStyle = TickStyle.None;
            _posVal.Text = "50%";
            _posVal.TextAlign = ContentAlignment.MiddleLeft;

            _lblPosHint.Text = "默认50%＝正中线，可微调";
            _lblPosHint.ForeColor = Color.FromArgb(130, 130, 130);
            _lblPosHint.AutoEllipsis = true;

            _lblWork.Text = "工作方式：横版原图沿竖中线裁成左右两张，每张独占一页A4并撑满，打印字号最大。";
            _lblWork.ForeColor = Color.FromArgb(43, 87, 154);
            _lblWork.AutoEllipsis = true;

            t.Controls.Add(MkLabel("裁切方式"), 0, 0);
            t.Controls.Add(_mode, 1, 0);
            t.Controls.Add(MkLabel("清晰度"), 2, 0);
            t.Controls.Add(_quality, 3, 0);
            t.Controls.Add(MkLabel("页面排布"), 4, 0);
            t.Controls.Add(_layout, 5, 0);

            t.Controls.Add(MkLabel("裁切位置"), 0, 1);
            t.SetColumnSpan(_pos, 3);
            t.Controls.Add(_pos, 1, 1);
            t.Controls.Add(_posVal, 4, 1);
            t.Controls.Add(_lblPosHint, 5, 1);

            t.SetColumnSpan(_lblWork, 6);
            t.Controls.Add(_lblWork, 0, 3);

            _chkLandscape.Text = "不裁切的横图：自动转横向 A4 撑满（三栏卷面推荐）";
            _chkLandscape.AutoSize = true;
            _chkLandscape.Checked = true;
            _chkLandscape.Anchor = AnchorStyles.Left;
            _chkLandscape.Margin = new Padding(2, 4, 0, 0);
            t.SetColumnSpan(_chkLandscape, 6);
            t.Controls.Add(_chkLandscape, 0, 2);
            _tip.SetToolTip(_chkLandscape, "三栏等不便裁切的横图，整图放入时该页自动改用横向 A4（297×210mm），图片撑满页面，不再上下留白。打印时纸是横放的，字仍是正方向。");
            _chkLandscape.CheckedChanged += delegate { RefreshPreview(); };

            foreach (Control c in new Control[] { _mode, _quality, _layout })
            {
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                c.Margin = new Padding(2, 3, 8, 3);
            }
            _pos.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _pos.Margin = new Padding(2, 1, 8, 1);
            _posVal.Anchor = AnchorStyles.Left;
            _posVal.Width = 44;
            _posVal.Margin = new Padding(2, 7, 2, 7);
            _lblPosHint.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _lblPosHint.Margin = new Padding(2, 7, 0, 7);
            _lblWork.Dock = DockStyle.Fill;
            _lblWork.Margin = new Padding(2, 4, 0, 0);

            _optsPanel.Controls.Add(t);
            _pos.ValueChanged += delegate { _posVal.Text = _pos.Value + "%"; RefreshPreview(); };
            _mode.SelectedIndexChanged += delegate { RefreshPreview(); };
            _tip.SetToolTip(_pos, "默认 50%＝正中线；若中间有装订线/折痕，可微调让两半都干净");
            _tip.SetToolTip(_lblWork, "工作方式：横版原图沿竖中线裁成左、右两张 → 每张裁片独占一页 A4 并撑满页面，打印出来字号最大、最清晰。");
        }

        static Label MkLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(0, 6, 4, 6);
            return l;
        }

        void HookEvents()
        {
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;
            _list.DragEnter += MainForm_DragEnter;
            _list.DragDrop += MainForm_DragDrop;
            _previewHost.DragEnter += MainForm_DragEnter;
            _previewHost.DragDrop += MainForm_DragDrop;
            _optsPanel.DragEnter += MainForm_DragEnter;
            _optsPanel.DragDrop += MainForm_DragDrop;

            _btnAdd.Click += delegate { AddViaDialog(); };
            _btnRemove.Click += delegate { RemoveSelected(); };
            _btnClear.Click += delegate { _files.Clear(); RebuildList(); };
            _btnSort.Click += delegate { NaturalSort(); };
            _btnUp.Click += delegate { MoveSelection(-1); };
            _btnDown.Click += delegate { MoveSelection(1); };
            _btnOut.Click += delegate { PickOutPath(); };
            _btnOpenDir.Click += delegate { OpenOutputDir(); };
            _btnOpenFile.Click += delegate { OpenOutputFile(); };
            _btnRun.Click += delegate { RunInBackground(); };
            _list.SelectedIndexChanged += delegate { RefreshPreview(); };
            _tip.SetToolTip(_btnRun, "按列表顺序处理全部图片，生成 docx");
            _tip.SetToolTip(_btnOpenDir, "在资源管理器中打开输出 docx 所在目录");
            _tip.SetToolTip(_btnOpenFile, "用默认程序（Word/WPS）打开输出 docx");
        }

        // ---------- 文件列表 ----------
        void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                Array paths = (Array)e.Data.GetData(DataFormats.FileDrop);
                if (paths == null) return;
                List<string> add = new List<string>();
                foreach (object p in paths)
                {
                    string path = (string)p;
                    if (Directory.Exists(path))
                    {
                        foreach (string f in Directory.GetFiles(path))
                            if (ImageUtil.IsAllowed(f)) add.Add(f);
                    }
                    else if (ImageUtil.IsAllowed(path)) add.Add(path);
                }
                AddFiles(add);
            }
            catch (Exception ex) { Log("拖放失败：" + ex.Message); }
        }

        void AddViaDialog()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Title = "选择试卷原图";
                dlg.Filter = ImageUtil.FilterText();
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    List<string> list = new List<string>();
                    for (int i = 0; i < dlg.FileNames.Length; i++) list.Add(dlg.FileNames[i]);
                    AddFiles(list);
                }
            }
        }

        void AddFiles(List<string> paths)
        {
            int added = 0, dup = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                string full;
                try { full = Path.GetFullPath(paths[i]); } catch { continue; }
                if (IndexOfFile(full) >= 0) { dup++; continue; }
                if (!ImageUtil.IsAllowed(full)) continue;
                _files.Add(full);
                added++;
            }
            if (added > 0)
            {
                _files.Sort(NatString.CmpPath);
                int keep = _list.Items.Count == 0 ? 0 : _list.SelectedIndex;
                RebuildList();
                if (keep > 0 && keep < _list.Items.Count) _list.SelectedIndex = keep;
                Log("已添加 " + added + " 张" + (dup > 0 ? "（重复 " + dup + " 张已跳过）" : "") + "，按文件名智能排序；顺序不对可用“上移/下移”调整。");
            }
            else if (dup > 0) Log("这些图片已经在列表里了。");
            else Log("没有可识别的图片（支持 jpg/png/bmp/gif/tif）。");
        }

        void RebuildList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            for (int i = 0; i < _files.Count; i++)
                _list.Items.Add((i + 1).ToString(CultureInfo.InvariantCulture) + ".  " + Path.GetFileName(_files[i]));
            _list.EndUpdate();
            if (_list.Items.Count > 0 && _list.SelectedIndex < 0) _list.SelectedIndex = 0;
            _cachedFile = null;
            _previewHost.Invalidate();
        }

        void RemoveSelected()
        {
            List<int> idx = new List<int>();
            foreach (int i in _list.SelectedIndices) idx.Add(i);
            idx.Sort(delegate(int a, int b) { return b - a; });
            for (int k = 0; k < idx.Count; k++)
                if (idx[k] >= 0 && idx[k] < _files.Count) _files.RemoveAt(idx[k]);
            RebuildList();
        }

        void NaturalSort()
        {
            _files.Sort(NatString.CmpPath);
            RebuildList();
            Log("已按文件名智能排序（数字按大小识别：第2页排在第10页前面）。");
        }

        void MoveSelection(int delta)
        {
            if (_list.SelectedIndices.Count == 0) return;
            List<int> sel = new List<int>();
            foreach (int i in _list.SelectedIndices) sel.Add(i);
            if (delta < 0)
            {
                sel.Sort();
                for (int k = 0; k < sel.Count; k++)
                {
                    int i = sel[k];
                    if (i == 0 || ContainsIdx(sel, i - 1)) continue;
                    SwapData(i - 1, i); sel[k] = i - 1;
                }
            }
            else
            {
                sel.Sort(delegate(int a, int b) { return b - a; });
                for (int k = 0; k < sel.Count; k++)
                {
                    int i = sel[k];
                    if (i >= _files.Count - 1 || ContainsIdx(sel, i + 1)) continue;
                    SwapData(i, i + 1); sel[k] = i + 1;
                }
            }
            RebuildList();
            _list.BeginUpdate();
            foreach (int i in sel)
                if (i >= 0 && i < _list.Items.Count) _list.SetSelected(i, true);
            _list.EndUpdate();
            if (sel.Count > 0) _list.SelectedIndex = Math.Min(Math.Max(0, sel[0]), _list.Items.Count - 1);
        }

        static bool ContainsIdx(List<int> list, int v)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == v) return true;
            return false;
        }

        void SwapData(int a, int b)
        {
            string t = _files[a]; _files[a] = _files[b]; _files[b] = t;
        }

        int IndexOfFile(string full)
        {
            for (int i = 0; i < _files.Count; i++)
                if (string.Equals(_files[i], full, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        // ---------- 输出路径 ----------
        void SetPlaceholder()
        {
            _outBox.ForeColor = Color.Gray;
            _outBox.Text = Placeholder;
            _outBox.GotFocus += delegate
            {
                if (_outBox.Text == Placeholder) { _outBox.Text = ""; _outBox.ForeColor = SystemColors.WindowText; }
            };
            _outBox.LostFocus += delegate
            {
                if (_outBox.Text.Length == 0) { _outBox.Text = Placeholder; _outBox.ForeColor = Color.Gray; }
            };
        }

        string OutPathOrEmpty()
        {
            string t = _outBox.Text.Trim();
            if (t.Length == 0 || t == Placeholder) return "";
            return t;
        }

        // 科目快捷键：把输出框填成“今日日期-科目”（若已是完整路径则只替换文件名部分）
        void FillSubjectName(string subjectKey)
        {
            string newName = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + subjectKey + ".docx";
            string cur = OutPathOrEmpty();
            if (cur.Length > 0 && (cur.Contains("\\") || cur.Contains("/")))
            {
                string dir = Path.GetDirectoryName(cur.TrimEnd('\\', '/'));
                _outBox.Text = string.IsNullOrEmpty(dir) ? newName : Path.Combine(dir, newName);
            }
            else
            {
                _outBox.Text = newName;
            }
            _outBox.ForeColor = SystemColors.WindowText;
            _outBox.SelectionStart = _outBox.TextLength;
        }

        void PickOutPath()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "选择 docx 保存位置";
                dlg.Filter = "Word 文档 (*.docx)|*.docx";
                dlg.FileName = SuggestedOutName();
                try { if (_files.Count > 0) dlg.InitialDirectory = Path.GetDirectoryName(_files[0]); } catch { }
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _outBox.Text = dlg.FileName;
                    _outBox.ForeColor = SystemColors.WindowText;
                }
            }
        }

        static string SuggestedOutName()
        {
            // 默认名：今日日期-自定义.docx（横线后由用户在保存框里填写）
            return DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-.docx";
        }

        // 解析最终输出 docx 的路径；无法确定（无图片且未填路径）或路径非法时返回空串。
        string ResolveOutPath()
        {
            string t = OutPathOrEmpty();
            try
            {
                if (t.Length == 0)
                {
                    if (_files.Count == 0) return "";
                    return Path.Combine(SafeDir(_files[0]), SuggestedOutName());
                }
                string dir;
                try { dir = Path.GetDirectoryName(t.TrimEnd('\\', '/')); }
                catch { dir = null; }
                if (string.IsNullOrEmpty(dir))
                {
                    if (_files.Count == 0) return "";
                    t = Path.Combine(SafeDir(_files[0]), t);   // 只填了文件名：落到图片所在文件夹
                }
                if (!t.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) t += ".docx";
                return t;
            }
            catch { return ""; }   // 非法字符等：由调用方提示，不弹未处理异常
        }

        void OpenOutputDir()
        {
            string path = ResolveOutPath();
            if (path.Length == 0) { BeepTip("还没有可用的输出位置：请先添加图片，或检查输出路径是否合法。"); return; }
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { BeepTip("目录不存在：" + dir); return; }
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("explorer.exe");
                psi.Arguments = "\"" + dir + "\"";
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex) { BeepTip("打开目录失败：" + ex.Message); }
        }

        void OpenOutputFile()
        {
            string path = ResolveOutPath();
            if (path.Length == 0) { BeepTip("还没有可打开的文件：请先添加图片，或检查输出路径是否合法。"); return; }
            if (!File.Exists(path))
            {
                BeepTip("docx 还没生成：" + path + "。请先点「执行」生成。");
                return;
            }
            try { System.Diagnostics.Process.Start(path); }
            catch (Exception ex) { BeepTip("打开文件失败：" + ex.Message); OpenInFolder(path); }
        }

        Config CurrentConfig()
        {
            Config cfg = new Config();
            cfg.Mode = (SplitMode)Math.Max(0, Math.Min(3, _mode.SelectedIndex));
            cfg.Position = _pos.Value;
            cfg.Quality = _quality.SelectedIndex == 0 ? 150
                        : _quality.SelectedIndex == 1 ? 200
                        : _quality.SelectedIndex == 2 ? 300 : 0;
            if (cfg.Quality == 0) cfg.MaxSourceSide = 0;
            cfg.TwoUp = _layout.SelectedIndex == 1;
            cfg.AutoLandscape = _chkLandscape.Checked;
            return cfg;
        }

        // ---------- 预览 ----------
        void RefreshPreview()
        {
            if (_previewHost != null) _previewHost.Invalidate();
        }

        void PreviewHost_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle box = _previewHost.ClientRectangle;
            g.Clear(Color.FromArgb(236, 238, 242));
            if (_files.Count == 0)
            {
                DrawCentered(g, box, "把试卷原图拖到这里，或点“添加图片”", Color.FromArgb(150, 150, 150), 13F);
                return;
            }
            int sel = _list.SelectedIndex >= 0 ? _list.SelectedIndex : 0;
            if (sel >= _files.Count) sel = _files.Count - 1;
            string file = _files[sel];

            if (!string.Equals(_cachedFile, file, StringComparison.OrdinalIgnoreCase))
            {
                if (_cachedImg != null) { _cachedImg.Dispose(); _cachedImg = null; }
                try { _cachedImg = ImageUtil.Load(file); _cachedFile = file; }
                catch (Exception ex)
                {
                    DrawCentered(g, box, "无法读取：" + ex.Message, Color.Firebrick, 11F);
                    return;
                }
            }
            if (_cachedImg == null) return;

            Image img = _cachedImg;
            SplitMode mode = (SplitMode)Math.Max(0, Math.Min(3, _mode.SelectedIndex));
            List<Rectangle> cuts = Cutter.Plan(img.Width, img.Height, mode, _pos.Value);
            bool vertical = Cutter.IsVertical(cuts);

            double availH = Math.Max(60, box.Height - 70);
            string caption;
            bool uncut = cuts.Count == 1;
            bool ls = uncut && img.Width > img.Height && _chkLandscape.Checked;

            if (uncut)
            {
                // 画出真实纸型（A4 竖版 / 自动横向），图片按版心等比放入，留白一目了然
                double pageWmm = ls ? 297.0 : 210.0, pageHmm = ls ? 210.0 : 297.0;
                double pageS = Math.Min((box.Width - 28) / pageWmm, availH / pageHmm);
                int pw = Math.Max(1, (int)(pageWmm * pageS)), ph = Math.Max(1, (int)(pageHmm * pageS));
                int qx = box.X + (box.Width - pw) / 2;
                int qy = box.Y + 42 + (int)((availH - ph) / 2);
                if (qy < box.Y + 42) qy = box.Y + 42;
                using (SolidBrush wb = new SolidBrush(Color.White)) g.FillRectangle(wb, qx, qy, pw, ph);
                double marginPx = 5.0 * pageS;
                double textW = Math.Max(1, pw - 2 * marginPx), textH = Math.Max(1, ph - 2 * marginPx);
                double is2 = Math.Min(textW / img.Width, textH / img.Height) * 0.96;
                int iw = Math.Max(1, (int)(img.Width * is2)), ih = Math.Max(1, (int)(img.Height * is2));
                g.DrawImage(img, qx + (pw - iw) / 2, qy + (ph - ih) / 2, iw, ih);
                g.DrawRectangle(Pens.Silver, qx, qy, pw, ph);
                caption = Path.GetFileName(file) + "   →   整页放入 1 页 A4（未裁切）";
                caption += ls ? "，已自动转横向撑满" : "，竖版居中";
            }
            else
            {
                double s = Math.Min((box.Width - 28) / (double)img.Width, availH / (double)img.Height);
                int dw = Math.Max(1, (int)(img.Width * s)), dh = Math.Max(1, (int)(img.Height * s));
                int px = box.X + (box.Width - dw) / 2, py = box.Y + 42;
                Rectangle dest = new Rectangle(px, py, dw, dh);
                g.DrawImage(img, dest);
                g.DrawRectangle(Pens.Gray, dest);
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    if (vertical)
                    {
                        int cx = px + (int)Math.Round((double)cuts[0].Width / img.Width * dw);
                        g.DrawLine(pen, cx, py, cx, py + dh);
                    }
                    else
                    {
                        int cy = py + (int)Math.Round((double)cuts[0].Height / img.Height * dh);
                        g.DrawLine(pen, px, cy, px + dw, cy);
                    }
                }
                caption = Path.GetFileName(file) + "   →   裁成" + (vertical ? "左、右两张" : "上、下两张");
                caption += _cfgTwoUp() ? "，上下排在 1 页 A4" : "，各独占 1 页 A4";
            }
            DrawTop(g, box, caption);
        }

        bool _cfgTwoUp()
        {
            return _layout != null && _layout.SelectedIndex == 1;
        }

        void DrawTop(Graphics g, Rectangle box, string text)
        {
            using (Font f = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(43, 87, 154)))
                g.DrawString(text, f, b, box.X + 10, box.Y + 8);
        }

        static void DrawCentered(Graphics g, Rectangle box, string text, Color color, float size)
        {
            using (Font f = new Font("Microsoft YaHei UI", size))
            using (SolidBrush b = new SolidBrush(color))
            {
                SizeF sz = g.MeasureString(text, f);
                g.DrawString(text, f, b, box.X + (box.Width - sz.Width) / 2, box.Y + (box.Height - sz.Height) / 2);
            }
        }

        // ---------- 执行 ----------
        void RunInBackground()
        {
            if (_busy) { BeepTip("正在处理中，请稍候…"); return; }
            if (_files.Count == 0)
            {
                MessageBox.Show(this, "请先添加试卷原图：点“添加图片”，或直接把图片/文件夹拖进窗口。",
                    "还没有图片", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            string outPath = ResolveOutPath();

            Config cfg = CurrentConfig();
            string[] files = _files.ToArray();
            _busy = true;
            _btnRun.Enabled = false;
            _btnRun.Text = "正在处理…";
            Log("开始处理 " + files.Length + " 张原图（模式：" + _mode.Text + "，" + _quality.Text + "，" + _layout.Text + "）");

            Thread th = new Thread(delegate()
            {
                bool ok = false;
                string msg = null, finalPath = null;
                int pageCount = 0;
                long size = 0;
                try
                {
                    MediaStore.Clear();
                    List<string> errors;
                    List<Page> pgs = Sizer.Build(files, cfg, delegate(string line) { UiLog("    " + line); }, out errors);
                    if (pgs.Count == 0)
                    {
                        string detail = errors.Count > 0 ? string.Join("\n", errors.ToArray()) : "没有可处理的图片。";
                        throw new InvalidOperationException("全部图片处理失败：\n" + detail);
                    }
                    finalPath = DocxBuilder.Write(outPath, pgs);
                    pageCount = pgs.Count;
                    size = MediaStore.TotalBytes;
                    if (errors.Count > 0) UiLog("注意：有 " + errors.Count + " 张图片读取失败被跳过。");
                    ok = true;
                }
                catch (Exception ex) { msg = "失败：" + ex.Message; }

                bool o = ok; string m = msg; string fp = finalPath; int pn = pageCount; long sz = size;
                Action ui = delegate
                {
                    _busy = false;
                    _btnRun.Enabled = true;
                    _btnRun.Text = "▶ 执行：裁开并生成 Word";
                    if (o)
                    {
                        _outBox.Text = fp;
                        _outBox.ForeColor = SystemColors.WindowText;
                        Log("完成：共 " + pn + " 页 A4，文件大小 " + MainForm.FriendlySize(sz));
                        Log("已保存：" + fp);
                        DoneDialog(fp, pn);
                    }
                    else
                    {
                        Log(m);
                        MessageBox.Show(this, m, "生成失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    MediaStore.Clear();
                    GC.Collect();
                };
                try { if (IsHandleCreated) BeginInvoke(ui); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        static string SafeDir(string file)
        {
            try { return Path.GetDirectoryName(Path.GetFullPath(file)); } catch { return Directory.GetCurrentDirectory(); }
        }

        void DoneDialog(string path, int pages)
        {
            try { Native.MessageBeep(0xFFFFFFFFu); } catch { }
            DialogResult r = MessageBox.Show(this,
                "已生成 Word 文档（" + pages + " 页 A4）：\n\n" + path + "\n\n要现在打开检查吗？",
                "完成", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes)
            {
                try { System.Diagnostics.Process.Start(path); }
                catch { OpenInFolder(path); }
            }
        }

        public static string FriendlySize(long bytes)
        {
            if (bytes >= 1024 * 1024) return (bytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB";
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        void OpenInFolder(string path)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("explorer.exe");
                psi.Arguments = "/select,\"" + path + "\"";
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        void BeepTip(string text)
        {
            Log(text);
            try { Native.MessageBeep(0xFFFFFFFFu); } catch { }
        }

        void Log(string text)
        {
            UiLog("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + text);
        }

        void UiLog(string text)
        {
            Action a = delegate
            {
                try
                {
                    if (_log.TextLength > 60000) _log.Clear();
                    _log.AppendText(text + Environment.NewLine);
                    _log.SelectionStart = _log.TextLength;
                    _log.ScrollToCaret();
                }
                catch { }
            };
            try { if (IsHandleCreated) BeginInvoke(a); } catch { }
        }
    }
}
