using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RepairServiceTracker
{
    // ===================== MODEL =====================
    public class RepairOrder
    {
        public string Id { get; set; }
        public string CustomerName { get; set; }
        public string Phone { get; set; }
        public string DeviceType { get; set; }
        public string Description { get; set; }
        public DateTime ReceivedDate { get; set; }
        public decimal EstimatedCost { get; set; }
        public string Status { get; set; }
        public string ImagePath { get; set; }
    }

    // ===================== MAIN FORM =====================
    public class MainForm : Form
    {
        static readonly string[] DeviceTypes = { "Điện thoại", "Laptop", "Máy tính bảng", "Tivi", "Máy in", "Khác" };
        static readonly string[] Statuses = { "Mới tiếp nhận", "Đang sửa", "Chờ linh kiện", "Hoàn tất", "Đã hủy" };
        const string AllStatus = "Tất cả";

        readonly List<RepairOrder> _all = new List<RepairOrder>();
        readonly BindingList<RepairOrder> _view = new BindingList<RepairOrder>();
        readonly BindingSource _bs = new BindingSource();
        RepairOrder _selected;
        string _imagePath;      // ảnh đang hiển thị trên form
        bool _suppress;

        TextBox txtId, txtName, txtPhone, txtCost, txtDesc;
        ComboBox cboDevice, cboStatus;
        DateTimePicker dtpReceived;
        PictureBox pic;
        Button btnPickImage, btnClearImage, btnNew, btnAdd, btnUpdate, btnDelete;
        ErrorProvider errors;

        TextBox txtSearch;
        ComboBox cboFilter;
        Button btnClearFilter;

        DataGridView grid;
        ToolStripStatusLabel lblStatus;

        public MainForm()
        {
            Text = "Repair Service Tracker";
            Size = new Size(1200, 740);
            MinimumSize = new Size(1000, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);

            BuildUi();
            SeedData();
            WireEvents();
            ApplyFilter();
            ClearInputs();
        }

        // ===================== GIAO DIỆN =====================
        void BuildUi()
        {
            errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

            // Layout chính: [thông tin + ảnh] / [tìm kiếm, lọc] / [bảng]
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ---- Hàng trên: 2 cột (form nhập | ảnh) ----
            var top = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var input = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, RowCount = 6 };
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 6; i++) input.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtId = new TextBox();
            txtName = new TextBox();
            txtPhone = new TextBox();
            txtCost = new TextBox { TextAlign = HorizontalAlignment.Right };
            txtDesc = new TextBox { Multiline = true, Height = 60, ScrollBars = ScrollBars.Vertical };
            cboDevice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboDevice.Items.AddRange(DeviceTypes);
            cboStatus.Items.AddRange(Statuses);
            dtpReceived = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };

            AddRow(input, 0, "Mã đơn:", txtId, "Tên khách hàng:", txtName);
            AddRow(input, 1, "Số điện thoại:", txtPhone, "Loại thiết bị:", cboDevice);
            AddRow(input, 2, "Ngày tiếp nhận:", dtpReceived, "Trạng thái:", cboStatus);
            AddRow(input, 3, "Chi phí dự kiến:", txtCost, "", new Label());

            input.Controls.Add(MakeLabel("Mô tả lỗi:"), 0, 4);
            txtDesc.Dock = DockStyle.Fill;
            txtDesc.Margin = new Padding(3, 5, 3, 5);
            input.Controls.Add(txtDesc, 1, 4);
            input.SetColumnSpan(txtDesc, 3);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            btnNew = NewButton("Làm mới");
            btnAdd = NewButton("Thêm");
            btnUpdate = NewButton("Cập nhật");
            btnDelete = NewButton("Xóa");
            btnPanel.Controls.AddRange(new Control[] { btnNew, btnAdd, btnUpdate, btnDelete });
            input.Controls.Add(btnPanel, 1, 5);
            input.SetColumnSpan(btnPanel, 3);

            // ---- Khu vực ảnh ----
            var picPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, MinimumSize = new Size(220, 210), Margin = new Padding(10, 3, 3, 3) };
            picPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            picPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pic = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,   // giữ đúng tỷ lệ, không méo
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.WhiteSmoke
            };
            var picBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            btnPickImage = NewButton("Chọn ảnh...");
            btnClearImage = NewButton("Xóa ảnh");
            picBtns.Controls.AddRange(new Control[] { btnPickImage, btnClearImage });
            picPanel.Controls.Add(pic, 0, 0);
            picPanel.Controls.Add(picBtns, 0, 1);

            top.Controls.Add(input, 0, 0);
            top.Controls.Add(picPanel, 1, 0);

            // ---- Tìm kiếm / lọc ----
            var filter = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 8, 0, 4) };
            txtSearch = new TextBox { Width = 260 };
            cboFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            cboFilter.Items.Add(AllStatus);
            cboFilter.Items.AddRange(Statuses);
            cboFilter.SelectedIndex = 0;
            btnClearFilter = new Button { Text = "Bỏ lọc", AutoSize = true };
            filter.Controls.Add(new Label { Text = "Tìm (mã đơn / tên / SĐT):", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
            filter.Controls.Add(txtSearch);
            filter.Controls.Add(new Label { Text = "Trạng thái:", AutoSize = true, Margin = new Padding(15, 7, 3, 3) });
            filter.Controls.Add(cboFilter);
            filter.Controls.Add(btnClearFilter);

            // ---- DataGridView ----
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            grid.Columns.Add(Col("Id", "Mã đơn", 60));
            grid.Columns.Add(Col("CustomerName", "Khách hàng", 120));
            grid.Columns.Add(Col("Phone", "Số điện thoại", 90));
            grid.Columns.Add(Col("DeviceType", "Thiết bị", 90));
            grid.Columns.Add(Col("Description", "Mô tả lỗi", 180));
            var cDate = Col("ReceivedDate", "Ngày tiếp nhận", 85);
            cDate.DefaultCellStyle.Format = "dd/MM/yyyy";
            grid.Columns.Add(cDate);
            var cCost = Col("EstimatedCost", "Chi phí dự kiến", 95);
            cCost.DefaultCellStyle.Format = "N0";
            cCost.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(cCost);
            grid.Columns.Add(Col("Status", "Trạng thái", 90));

            _bs.DataSource = _view;
            grid.DataSource = _bs;

            main.Controls.Add(top, 0, 0);
            main.Controls.Add(filter, 0, 1);
            main.Controls.Add(grid, 0, 2);

            var statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            statusStrip.Items.Add(lblStatus);

            var menu = new MenuStrip();
            var mFile = new ToolStripMenuItem("Tệp");
            var mExport = new ToolStripMenuItem("Xuất danh sách đang hiển thị ra CSV...", null, (s, e) => ExportCsv());
            mExport.ShortcutKeys = Keys.Control | Keys.E;
            mFile.DropDownItems.Add(mExport);
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add(new ToolStripMenuItem("Thoát", null, (s, e) => Close()));
            menu.Items.Add(mFile);

            Controls.Add(main);
            Controls.Add(statusStrip);
            Controls.Add(menu);
            MainMenuStrip = menu;
        }

        static Button NewButton(string text)
        {
            return new Button { Text = text, AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
        }

        static Label MakeLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 8) };
        }

        static void AddRow(TableLayoutPanel t, int row, string l1, Control c1, string l2, Control c2)
        {
            t.Controls.Add(MakeLabel(l1), 0, row);
            c1.Dock = DockStyle.Fill;
            t.Controls.Add(c1, 1, row);
            t.Controls.Add(MakeLabel(l2), 2, row);
            c2.Dock = DockStyle.Fill;
            t.Controls.Add(c2, 3, row);
        }

        static DataGridViewTextBoxColumn Col(string prop, string header, float weight)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = prop,
                HeaderText = header,
                Name = "col" + prop,
                FillWeight = weight
            };
        }

        // ===================== DỮ LIỆU MẪU =====================
        void SeedData()
        {
            DateTime t = DateTime.Today;
            _all.Add(new RepairOrder { Id = "DH001", CustomerName = "Nguyễn Văn An", Phone = "0912345678", DeviceType = "Điện thoại", Description = "Vỡ màn hình, cảm ứng loạn", ReceivedDate = t.AddDays(-3), EstimatedCost = 1200000, Status = "Đang sửa" });
            _all.Add(new RepairOrder { Id = "DH002", CustomerName = "Trần Thị Bình", Phone = "0987654321", DeviceType = "Laptop", Description = "Không lên nguồn", ReceivedDate = t.AddDays(-5), EstimatedCost = 850000, Status = "Chờ linh kiện" });
            _all.Add(new RepairOrder { Id = "DH003", CustomerName = "Lê Minh Châu", Phone = "0903111222", DeviceType = "Máy in", Description = "Kẹt giấy, in mờ", ReceivedDate = t.AddDays(-8), EstimatedCost = 300000, Status = "Hoàn tất" });
            _all.Add(new RepairOrder { Id = "DH004", CustomerName = "Phạm Quốc Dũng", Phone = "0336789012", DeviceType = "Tivi", Description = "Có tiếng không có hình", ReceivedDate = t.AddDays(-1), EstimatedCost = 2500000, Status = "Mới tiếp nhận" });
            _all.Add(new RepairOrder { Id = "DH005", CustomerName = "Vũ Thanh Hà", Phone = "0977123456", DeviceType = "Máy tính bảng", Description = "Chai pin, sạc không vào", ReceivedDate = t.AddDays(-10), EstimatedCost = 450000, Status = "Đã hủy" });
        }

        // ===================== SỰ KIỆN =====================
        void WireEvents()
        {
            grid.SelectionChanged += (s, e) => LoadSelectedRow();
            grid.CellFormatting += Grid_CellFormatting;

            btnNew.Click += (s, e) => { grid.ClearSelection(); _selected = null; ClearInputs(); };
            btnAdd.Click += (s, e) => AddOrder();
            btnUpdate.Click += (s, e) => UpdateOrder();
            btnDelete.Click += (s, e) => DeleteOrder();
            btnPickImage.Click += (s, e) => PickImage();
            btnClearImage.Click += (s, e) => { _imagePath = null; ShowImage(null); };

            txtSearch.TextChanged += (s, e) => ApplyFilter();
            cboFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            btnClearFilter.Click += (s, e) => { txtSearch.Text = ""; cboFilter.SelectedIndex = 0; ApplyFilter(); };

            txtId.TextChanged += (s, e) => errors.SetError(txtId, "");
            txtName.TextChanged += (s, e) => errors.SetError(txtName, "");
            txtPhone.TextChanged += (s, e) => errors.SetError(txtPhone, "");
            txtCost.TextChanged += (s, e) => errors.SetError(txtCost, "");
            txtDesc.TextChanged += (s, e) => errors.SetError(txtDesc, "");

            FormClosed += (s, e) => { if (pic.Image != null) pic.Image.Dispose(); };
        }

        void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _view.Count) return;
            string st = _view[e.RowIndex].Status;
            if (st == "Hoàn tất") e.CellStyle.ForeColor = Color.DarkGreen;
            else if (st == "Đã hủy") e.CellStyle.ForeColor = Color.Gray;
            else if (st == "Chờ linh kiện") e.CellStyle.ForeColor = Color.DarkOrange;
        }

        // ===================== ẢNH =====================
        void PickImage()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Chọn ảnh thiết bị";
                dlg.Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Tất cả (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (ShowImage(dlg.FileName)) _imagePath = dlg.FileName;
            }
        }

        // Nạp ảnh qua bản sao trong bộ nhớ để không khóa file gốc
        bool ShowImage(string path)
        {
            Image newImg = null;
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var tmp = Image.FromStream(fs))
                        newImg = new Bitmap(tmp);
                }
                catch (Exception)
                {
                    if (File.Exists(path))   // file có nhưng không phải ảnh hợp lệ
                    {
                        MessageBox.Show("Không thể đọc file ảnh này.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    newImg = null;           // file đã bị xóa/di chuyển: hiển thị trống
                }
            }
            var old = pic.Image;
            pic.Image = newImg;
            if (old != null) old.Dispose();
            return true;
        }

        // ===================== LỌC / TÌM KIẾM =====================
        void ApplyFilter()
        {
            string kw = (txtSearch.Text ?? "").Trim();
            string st = cboFilter.SelectedItem == null ? AllStatus : cboFilter.SelectedItem.ToString();

            IEnumerable<RepairOrder> q = _all;
            if (kw.Length > 0)
                q = q.Where(o =>
                    o.Id.IndexOf(kw, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    o.CustomerName.IndexOf(kw, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    o.Phone.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
            if (st != AllStatus)
                q = q.Where(o => o.Status == st);

            var result = q.OrderBy(o => o.Id).ToList();

            _suppress = true;
            try
            {
                _view.RaiseListChangedEvents = false;
                _view.Clear();
                foreach (var o in result) _view.Add(o);
                _view.RaiseListChangedEvents = true;
                _view.ResetBindings();
                grid.ClearSelection();

                if (_selected != null)
                {
                    int idx = _view.IndexOf(_selected);
                    if (idx >= 0)
                    {
                        grid.Rows[idx].Selected = true;
                        grid.CurrentCell = grid.Rows[idx].Cells[0];
                    }
                }
            }
            finally { _suppress = false; }

            UpdateStatusBar();
            grid.Invalidate();
        }

        void UpdateStatusBar()
        {
            // Đơn "chưa hoàn tất" = không phải Hoàn tất và không phải Đã hủy
            decimal pending = _all.Where(o => o.Status != "Hoàn tất" && o.Status != "Đã hủy")
                                  .Sum(o => o.EstimatedCost);
            int pendingCount = _all.Count(o => o.Status != "Hoàn tất" && o.Status != "Đã hủy");
            lblStatus.Text = string.Format(
                "Số đơn theo bộ lọc: {0} / {1}  |  Đơn chưa hoàn tất: {2}  |  Tổng chi phí dự kiến (chưa hoàn tất): {3:N0} VNĐ",
                _view.Count, _all.Count, pendingCount, pending);
        }

        // ===================== NẠP / XÓA FORM =====================
        void LoadSelectedRow()
        {
            if (_suppress) return;
            if (grid.CurrentRow == null || !grid.CurrentRow.Selected) return;
            var o = grid.CurrentRow.DataBoundItem as RepairOrder;
            if (o == null) return;
            _selected = o;
            PopulateInputs(o);
        }

        void PopulateInputs(RepairOrder o)
        {
            errors.Clear();
            txtId.Text = o.Id;
            txtName.Text = o.CustomerName;
            txtPhone.Text = o.Phone;
            cboDevice.SelectedItem = o.DeviceType;
            txtDesc.Text = o.Description;
            dtpReceived.Value = o.ReceivedDate < dtpReceived.MinDate ? dtpReceived.MinDate : o.ReceivedDate;
            txtCost.Text = o.EstimatedCost.ToString("N0");
            cboStatus.SelectedItem = o.Status;
            _imagePath = o.ImagePath;
            ShowImage(o.ImagePath);
        }

        void ClearInputs()
        {
            errors.Clear();
            txtId.Text = NextId();
            txtName.Text = "";
            txtPhone.Text = "";
            txtDesc.Text = "";
            txtCost.Text = "0";
            cboDevice.SelectedIndex = 0;
            cboStatus.SelectedIndex = 0;
            dtpReceived.Value = DateTime.Today;
            _imagePath = null;
            ShowImage(null);
            txtName.Focus();
        }

        string NextId()
        {
            int max = 0;
            foreach (var o in _all)
            {
                int n;
                if (o.Id.StartsWith("DH", StringComparison.OrdinalIgnoreCase) && int.TryParse(o.Id.Substring(2), out n))
                    max = Math.Max(max, n);
            }
            return "DH" + (max + 1).ToString("000");
        }

        // ===================== KIỂM TRA DỮ LIỆU =====================
        bool TryParseCost(string text, out decimal cost)
        {
            return decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out cost);
        }

        bool ValidateInputs(bool isUpdate)
        {
            errors.Clear();
            bool ok = true;

            string id = txtId.Text.Trim();
            if (id.Length == 0)
            {
                errors.SetError(txtId, "Mã đơn không được để trống");
                ok = false;
            }
            else if (_all.Any(o => !ReferenceEquals(o, isUpdate ? _selected : null) &&
                                   string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                errors.SetError(txtId, "Mã đơn đã tồn tại");
                ok = false;
            }

            if (txtName.Text.Trim().Length == 0)
            {
                errors.SetError(txtName, "Tên khách hàng không được để trống");
                ok = false;
            }
            if (txtPhone.Text.Trim().Length == 0)
            {
                errors.SetError(txtPhone, "Số điện thoại không được để trống");
                ok = false;
            }
            if (txtDesc.Text.Trim().Length == 0)
            {
                errors.SetError(txtDesc, "Vui lòng mô tả lỗi của thiết bị");
                ok = false;
            }

            decimal cost;
            if (!TryParseCost(txtCost.Text, out cost))
            {
                errors.SetError(txtCost, "Chi phí phải là một số hợp lệ");
                ok = false;
            }
            else if (cost < 0)
            {
                errors.SetError(txtCost, "Chi phí dự kiến không được âm");
                ok = false;
            }

            if (cboDevice.SelectedIndex < 0) { errors.SetError(cboDevice, "Chọn loại thiết bị"); ok = false; }
            if (cboStatus.SelectedIndex < 0) { errors.SetError(cboStatus, "Chọn trạng thái"); ok = false; }

            return ok;
        }

        void CopyInputsTo(RepairOrder o)
        {
            decimal cost;
            TryParseCost(txtCost.Text, out cost);
            o.Id = txtId.Text.Trim();
            o.CustomerName = txtName.Text.Trim();
            o.Phone = txtPhone.Text.Trim();
            o.DeviceType = cboDevice.SelectedItem.ToString();
            o.Description = txtDesc.Text.Trim();
            o.ReceivedDate = dtpReceived.Value.Date;
            o.EstimatedCost = cost;
            o.Status = cboStatus.SelectedItem.ToString();
            o.ImagePath = _imagePath;
        }

        // ===================== THÊM / SỬA / XÓA =====================
        void AddOrder()
        {
            if (!ValidateInputs(false)) return;
            if (MessageBox.Show("Xác nhận thêm đơn " + txtId.Text.Trim() + "?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var o = new RepairOrder();
            CopyInputsTo(o);
            _all.Add(o);
            _selected = o;
            ApplyFilter();
            PopulateInputs(o);
        }

        void UpdateOrder()
        {
            if (_selected == null)
            {
                MessageBox.Show("Hãy chọn một đơn trong danh sách để cập nhật.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ValidateInputs(true)) return;
            if (MessageBox.Show("Xác nhận cập nhật đơn " + _selected.Id + "?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            CopyInputsTo(_selected);
            ApplyFilter();             // cập nhật bảng + tổng chi phí trên StatusStrip
            PopulateInputs(_selected);
        }

        void DeleteOrder()
        {
            if (_selected == null)
            {
                MessageBox.Show("Hãy chọn một đơn trong danh sách để xóa.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Bạn có chắc muốn xóa đơn " + _selected.Id + " - " + _selected.CustomerName + "?",
                "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _all.Remove(_selected);
            _selected = null;
            ApplyFilter();
            ClearInputs();
        }

        // ===================== XUẤT CSV =====================
        void ExportCsv()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "CSV (*.csv)|*.csv";
                dlg.FileName = "don_sua_chua_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("Mã đơn,Khách hàng,Số điện thoại,Loại thiết bị,Mô tả lỗi,Ngày tiếp nhận,Chi phí dự kiến,Trạng thái");
                foreach (var o in _view)   // chỉ xuất các đơn đang hiển thị
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        Csv(o.Id), Csv(o.CustomerName), Csv(o.Phone), Csv(o.DeviceType), Csv(o.Description),
                        Csv(o.ReceivedDate.ToString("dd/MM/yyyy")),
                        o.EstimatedCost.ToString("0.##", CultureInfo.InvariantCulture),
                        Csv(o.Status)
                    }));
                }
                try
                {
                    File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show("Đã xuất " + _view.Count + " đơn.", "Xuất CSV",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể ghi file: " + ex.Message, "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        static string Csv(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
