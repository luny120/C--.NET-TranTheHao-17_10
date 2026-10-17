using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LabEquipmentCheckout
{
    // ===================== MODEL =====================
    public class Loan
    {
        public string Id { get; set; }
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string Equipment { get; set; }
        public int Quantity { get; set; }
        public DateTime BorrowDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        // Tình trạng được TỰ TÍNH, không lưu thủ công
        public string Status
        {
            get
            {
                if (ReturnDate.HasValue) return "Đã trả";
                if (DueDate.Date < DateTime.Today) return "Quá hạn";
                return "Đang mượn";
            }
        }
    }

    // ===================== MAIN FORM =====================
    public class MainForm : Form
    {
        static readonly string[] Equipments =
        {
            "Máy hiện sóng (Oscilloscope)", "Bộ KIT Arduino", "Bộ KIT Raspberry Pi",
            "Đồng hồ vạn năng", "Máy hàn thiếc", "Laptop thực hành"
        };
        const string AllStatus = "Tất cả";

        readonly List<Loan> _all = new List<Loan>();
        readonly BindingList<Loan> _view = new BindingList<Loan>();
        readonly BindingSource _bs = new BindingSource();
        Loan _selected;
        bool _suppress;

        TextBox txtId, txtStudentId, txtStudentName, txtStatus;
        ComboBox cboEquipment;
        NumericUpDown numQty;
        DateTimePicker dtpBorrow, dtpDue, dtpReturn;
        CheckBox chkReturned;
        Button btnNew, btnAdd, btnUpdate, btnDelete, btnMarkReturned;
        ErrorProvider errors;

        TextBox txtSearch;
        ComboBox cboFilter;
        Button btnClearFilter;

        DataGridView grid;
        ToolStripStatusLabel lblStatus;

        public MainForm()
        {
            Text = "Lab Equipment Checkout";
            Size = new Size(1150, 700);
            MinimumSize = new Size(950, 580);
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

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ---- Khu vực nhập phiếu mượn ----
            var input = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, RowCount = 6 };
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 6; i++) input.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtId = new TextBox();
            txtStudentId = new TextBox();
            txtStudentName = new TextBox();
            txtStatus = new TextBox { ReadOnly = true, BackColor = SystemColors.Control };
            cboEquipment = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboEquipment.Items.AddRange(Equipments);
            numQty = new NumericUpDown { Minimum = 0, Maximum = 1000, Value = 1 };
            dtpBorrow = NewDatePicker();
            dtpDue = NewDatePicker();
            dtpReturn = NewDatePicker();
            dtpReturn.Enabled = false;
            dtpReturn.Width = 130;
            chkReturned = new CheckBox { Text = "Đã trả", AutoSize = true, Margin = new Padding(3, 6, 10, 3) };

            AddRow(input, 0, "Mã phiếu:", txtId, "Mã sinh viên:", txtStudentId);
            AddRow(input, 1, "Họ tên SV:", txtStudentName, "Thiết bị:", cboEquipment);
            AddRow(input, 2, "Số lượng:", numQty, "Ngày mượn:", dtpBorrow);
            AddRow(input, 3, "Hạn trả:", dtpDue, "Tình trạng:", txtStatus);

            var retPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            retPanel.Controls.Add(chkReturned);
            retPanel.Controls.Add(dtpReturn);
            input.Controls.Add(MakeLabel("Ngày trả TT:"), 0, 4);
            input.Controls.Add(retPanel, 1, 4);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            btnNew = NewButton("Làm mới");
            btnAdd = NewButton("Thêm");
            btnUpdate = NewButton("Cập nhật");
            btnDelete = NewButton("Xóa");
            btnMarkReturned = NewButton("Đánh dấu đã trả");
            btnPanel.Controls.AddRange(new Control[] { btnNew, btnAdd, btnUpdate, btnDelete, btnMarkReturned });
            input.Controls.Add(btnPanel, 1, 5);
            input.SetColumnSpan(btnPanel, 3);

            // ---- Tìm kiếm / lọc ----
            var filter = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 8, 0, 4) };
            txtSearch = new TextBox { Width = 250 };
            cboFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
            cboFilter.Items.AddRange(new object[] { AllStatus, "Đang mượn", "Quá hạn", "Đã trả" });
            cboFilter.SelectedIndex = 0;
            btnClearFilter = new Button { Text = "Bỏ lọc", AutoSize = true };
            filter.Controls.Add(new Label { Text = "Tìm (mã SV / thiết bị):", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
            filter.Controls.Add(txtSearch);
            filter.Controls.Add(new Label { Text = "Tình trạng:", AutoSize = true, Margin = new Padding(15, 7, 3, 3) });
            filter.Controls.Add(cboFilter);
            filter.Controls.Add(btnClearFilter);

            // ---- DataGridView (cột cấu hình thủ công) ----
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
            grid.Columns.Add(Col("Id", "Mã phiếu", 70));
            grid.Columns.Add(Col("StudentId", "Mã SV", 80));
            grid.Columns.Add(Col("StudentName", "Họ tên SV", 130));
            grid.Columns.Add(Col("Equipment", "Thiết bị", 170));
            grid.Columns.Add(Col("Quantity", "SL", 40));
            grid.Columns.Add(DateCol("BorrowDate", "Ngày mượn"));
            grid.Columns.Add(DateCol("DueDate", "Hạn trả"));
            grid.Columns.Add(DateCol("ReturnDate", "Ngày trả TT"));
            grid.Columns.Add(Col("Status", "Tình trạng", 80));

            _bs.DataSource = _view;
            grid.DataSource = _bs;

            main.Controls.Add(input, 0, 0);
            main.Controls.Add(filter, 0, 1);
            main.Controls.Add(grid, 0, 2);

            var statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            statusStrip.Items.Add(lblStatus);

            var menu = new MenuStrip();
            var mFile = new ToolStripMenuItem("Tệp");
            var mExport = new ToolStripMenuItem("Xuất phiếu đang lọc ra CSV...", null, (s, e) => ExportCsv());
            mExport.ShortcutKeys = Keys.Control | Keys.E;
            mFile.DropDownItems.Add(mExport);
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add(new ToolStripMenuItem("Thoát", null, (s, e) => Close()));
            menu.Items.Add(mFile);

            Controls.Add(main);        // Fill add trước
            Controls.Add(statusStrip);
            Controls.Add(menu);
            MainMenuStrip = menu;
        }

        static DateTimePicker NewDatePicker()
        {
            return new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
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

        static DataGridViewTextBoxColumn DateCol(string prop, string header)
        {
            var c = Col(prop, header, 85);
            c.DefaultCellStyle.Format = "dd/MM/yyyy";
            c.DefaultCellStyle.NullValue = "";   // ngày trả trống khi chưa trả
            return c;
        }

        // ===================== DỮ LIỆU MẪU =====================
        void SeedData()
        {
            DateTime t = DateTime.Today;
            _all.Add(new Loan { Id = "PM001", StudentId = "SV2021001", StudentName = "Nguyễn Văn An", Equipment = Equipments[1], Quantity = 1, BorrowDate = t.AddDays(-10), DueDate = t.AddDays(-3), ReturnDate = null });                  // quá hạn
            _all.Add(new Loan { Id = "PM002", StudentId = "SV2021002", StudentName = "Trần Thị Bình", Equipment = Equipments[3], Quantity = 2, BorrowDate = t.AddDays(-2), DueDate = t.AddDays(5), ReturnDate = null });                    // đang mượn
            _all.Add(new Loan { Id = "PM003", StudentId = "SV2021003", StudentName = "Lê Minh Châu", Equipment = Equipments[0], Quantity = 1, BorrowDate = t.AddDays(-14), DueDate = t.AddDays(-7), ReturnDate = t.AddDays(-8) });             // đã trả
            _all.Add(new Loan { Id = "PM004", StudentId = "SV2021004", StudentName = "Phạm Quốc Dũng", Equipment = Equipments[5], Quantity = 1, BorrowDate = t.AddDays(-20), DueDate = t.AddDays(-1), ReturnDate = null });                 // quá hạn
            _all.Add(new Loan { Id = "PM005", StudentId = "SV2021005", StudentName = "Vũ Thanh Hà", Equipment = Equipments[2], Quantity = 3, BorrowDate = t, DueDate = t.AddDays(7), ReturnDate = null });                              // đang mượn
        }

        // ===================== SỰ KIỆN =====================
        void WireEvents()
        {
            grid.SelectionChanged += (s, e) => LoadSelectedRow();
            grid.CellFormatting += Grid_CellFormatting;

            btnNew.Click += (s, e) => { grid.ClearSelection(); _selected = null; ClearInputs(); };
            btnAdd.Click += (s, e) => AddLoan();
            btnUpdate.Click += (s, e) => UpdateLoan();
            btnDelete.Click += (s, e) => DeleteLoan();
            btnMarkReturned.Click += (s, e) => MarkReturned();

            txtSearch.TextChanged += (s, e) => ApplyFilter();
            cboFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            btnClearFilter.Click += (s, e) => { txtSearch.Text = ""; cboFilter.SelectedIndex = 0; ApplyFilter(); };

            chkReturned.CheckedChanged += (s, e) => { dtpReturn.Enabled = chkReturned.Checked; RefreshPreviewStatus(); };
            dtpDue.ValueChanged += (s, e) => { errors.SetError(dtpDue, ""); RefreshPreviewStatus(); };
            dtpBorrow.ValueChanged += (s, e) => { errors.SetError(dtpBorrow, ""); errors.SetError(dtpDue, ""); errors.SetError(dtpReturn, ""); };
            dtpReturn.ValueChanged += (s, e) => { errors.SetError(dtpReturn, ""); RefreshPreviewStatus(); };

            txtId.TextChanged += (s, e) => errors.SetError(txtId, "");
            txtStudentId.TextChanged += (s, e) => errors.SetError(txtStudentId, "");
            txtStudentName.TextChanged += (s, e) => errors.SetError(txtStudentName, "");
            cboEquipment.SelectedIndexChanged += (s, e) => errors.SetError(cboEquipment, "");
            numQty.ValueChanged += (s, e) => errors.SetError(numQty, "");
        }

        void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _view.Count) return;
            string st = _view[e.RowIndex].Status;
            if (st == "Quá hạn") { e.CellStyle.ForeColor = Color.Firebrick; e.CellStyle.Font = new Font(grid.Font, FontStyle.Bold); }
            else if (st == "Đã trả") e.CellStyle.ForeColor = Color.DarkGreen;
        }

        // Hiển thị tình trạng dự kiến theo dữ liệu đang nhập
        void RefreshPreviewStatus()
        {
            if (chkReturned.Checked) txtStatus.Text = "Đã trả";
            else txtStatus.Text = dtpDue.Value.Date < DateTime.Today ? "Quá hạn" : "Đang mượn";
        }

        // ===================== LỌC / TÌM KIẾM =====================
        void ApplyFilter()
        {
            string kw = (txtSearch.Text ?? "").Trim();
            string st = cboFilter.SelectedItem == null ? AllStatus : cboFilter.SelectedItem.ToString();

            IEnumerable<Loan> q = _all;
            if (kw.Length > 0)
                q = q.Where(l =>
                    l.StudentId.IndexOf(kw, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    l.Equipment.IndexOf(kw, StringComparison.CurrentCultureIgnoreCase) >= 0);
            if (st != AllStatus)
                q = q.Where(l => l.Status == st);   // Status tự tính theo ngày hiện tại

            var result = q.OrderBy(l => l.Id).ToList();

            _suppress = true;
            try
            {
                _view.RaiseListChangedEvents = false;
                _view.Clear();
                foreach (var l in result) _view.Add(l);
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
            int overdue = _all.Count(l => l.Status == "Quá hạn");
            lblStatus.Text = string.Format("Đang hiển thị: {0}  |  Tổng số phiếu: {1}  |  Số phiếu quá hạn: {2}",
                _view.Count, _all.Count, overdue);
        }

        // ===================== NẠP / XÓA FORM =====================
        void LoadSelectedRow()
        {
            if (_suppress) return;
            if (grid.CurrentRow == null || !grid.CurrentRow.Selected) return;
            var l = grid.CurrentRow.DataBoundItem as Loan;
            if (l == null) return;
            _selected = l;
            PopulateInputs(l);
        }

        void PopulateInputs(Loan l)
        {
            errors.Clear();
            txtId.Text = l.Id;
            txtStudentId.Text = l.StudentId;
            txtStudentName.Text = l.StudentName;
            cboEquipment.SelectedItem = l.Equipment;
            numQty.Value = Math.Min(numQty.Maximum, Math.Max(numQty.Minimum, l.Quantity));
            dtpBorrow.Value = l.BorrowDate;
            dtpDue.Value = l.DueDate;
            chkReturned.Checked = l.ReturnDate.HasValue;
            dtpReturn.Value = l.ReturnDate ?? DateTime.Today;
            dtpReturn.Enabled = l.ReturnDate.HasValue;
            txtStatus.Text = l.Status;
        }

        void ClearInputs()
        {
            errors.Clear();
            txtId.Text = NextId();
            txtStudentId.Text = "";
            txtStudentName.Text = "";
            cboEquipment.SelectedIndex = -1;
            numQty.Value = 1;
            dtpBorrow.Value = DateTime.Today;
            dtpDue.Value = DateTime.Today.AddDays(7);
            chkReturned.Checked = false;
            dtpReturn.Value = DateTime.Today;
            dtpReturn.Enabled = false;
            RefreshPreviewStatus();
            txtStudentId.Focus();
        }

        string NextId()
        {
            int max = 0;
            foreach (var l in _all)
            {
                if (l.Id.StartsWith("PM", StringComparison.OrdinalIgnoreCase))
                {
                    int n;
                    if (int.TryParse(l.Id.Substring(2), out n)) max = Math.Max(max, n);
                }
            }
            return "PM" + (max + 1).ToString("000");
        }

        // ===================== KIỂM TRA DỮ LIỆU =====================
        bool ValidateInputs(bool isUpdate)
        {
            errors.Clear();
            bool ok = true;

            string id = txtId.Text.Trim();
            if (id.Length == 0)
            {
                errors.SetError(txtId, "Mã phiếu không được để trống");
                ok = false;
            }
            else if (_all.Any(l => !ReferenceEquals(l, isUpdate ? _selected : null) &&
                                   string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                errors.SetError(txtId, "Mã phiếu đã tồn tại");
                ok = false;
            }

            if (txtStudentId.Text.Trim().Length == 0)
            {
                errors.SetError(txtStudentId, "Mã sinh viên không được để trống");
                ok = false;
            }
            if (txtStudentName.Text.Trim().Length == 0)
            {
                errors.SetError(txtStudentName, "Họ tên sinh viên không được để trống");
                ok = false;
            }
            if (cboEquipment.SelectedIndex < 0)
            {
                errors.SetError(cboEquipment, "Vui lòng chọn thiết bị");
                ok = false;
            }
            if (numQty.Value <= 0)
            {
                errors.SetError(numQty, "Số lượng phải lớn hơn 0");
                ok = false;
            }
            if (dtpDue.Value.Date < dtpBorrow.Value.Date)
            {
                errors.SetError(dtpDue, "Hạn trả không được trước ngày mượn");
                ok = false;
            }
            if (chkReturned.Checked && dtpReturn.Value.Date < dtpBorrow.Value.Date)
            {
                errors.SetError(dtpReturn, "Ngày trả thực tế không được trước ngày mượn");
                ok = false;
            }
            return ok;
        }

        void CopyInputsTo(Loan l)
        {
            l.Id = txtId.Text.Trim();
            l.StudentId = txtStudentId.Text.Trim();
            l.StudentName = txtStudentName.Text.Trim();
            l.Equipment = cboEquipment.SelectedItem.ToString();
            l.Quantity = (int)numQty.Value;
            l.BorrowDate = dtpBorrow.Value.Date;
            l.DueDate = dtpDue.Value.Date;
            l.ReturnDate = chkReturned.Checked ? (DateTime?)dtpReturn.Value.Date : null;
        }

        // ===================== THÊM / SỬA / XÓA / TRẢ =====================
        void AddLoan()
        {
            if (!ValidateInputs(false)) return;
            if (MessageBox.Show("Xác nhận thêm phiếu mượn " + txtId.Text.Trim() + "?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var l = new Loan();
            CopyInputsTo(l);
            _all.Add(l);
            _selected = l;
            ApplyFilter();
            PopulateInputs(l);
        }

        void UpdateLoan()
        {
            if (_selected == null)
            {
                MessageBox.Show("Hãy chọn một phiếu trong danh sách để cập nhật.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ValidateInputs(true)) return;
            if (MessageBox.Show("Xác nhận cập nhật phiếu " + _selected.Id + "?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            CopyInputsTo(_selected);
            ApplyFilter();
            PopulateInputs(_selected);
        }

        void DeleteLoan()
        {
            if (_selected == null)
            {
                MessageBox.Show("Hãy chọn một phiếu trong danh sách để xóa.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Bạn có chắc muốn xóa phiếu " + _selected.Id + " (" + _selected.StudentName + ")?",
                "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _all.Remove(_selected);
            _selected = null;
            ApplyFilter();
            ClearInputs();
        }

        // Đánh dấu đã trả: ngày trả thực tế = hôm nay
        void MarkReturned()
        {
            if (_selected == null)
            {
                MessageBox.Show("Hãy chọn một phiếu đang mượn để đánh dấu đã trả.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_selected.ReturnDate.HasValue)
            {
                MessageBox.Show("Phiếu " + _selected.Id + " đã được trả ngày " +
                    _selected.ReturnDate.Value.ToString("dd/MM/yyyy") + ".", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (DateTime.Today < _selected.BorrowDate.Date)
            {
                MessageBox.Show("Ngày trả thực tế không được trước ngày mượn.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show("Xác nhận phiếu " + _selected.Id + " đã được trả hôm nay (" +
                DateTime.Today.ToString("dd/MM/yyyy") + ")?", "Xác nhận trả",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _selected.ReturnDate = DateTime.Today;
            ApplyFilter();             // bảng + StatusStrip cập nhật ngay
            PopulateInputs(_selected); // form hiển thị ngày trả và tình trạng mới
        }

        // ===================== XUẤT CSV =====================
        void ExportCsv()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "CSV (*.csv)|*.csv";
                dlg.FileName = "phieu_muon_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("Mã phiếu,Mã sinh viên,Họ tên,Thiết bị,Số lượng,Ngày mượn,Hạn trả,Ngày trả thực tế,Tình trạng");
                foreach (var l in _view)   // chỉ xuất các phiếu đang lọc
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        Csv(l.Id), Csv(l.StudentId), Csv(l.StudentName), Csv(l.Equipment),
                        l.Quantity.ToString(CultureInfo.InvariantCulture),
                        Csv(l.BorrowDate.ToString("dd/MM/yyyy")),
                        Csv(l.DueDate.ToString("dd/MM/yyyy")),
                        Csv(l.ReturnDate.HasValue ? l.ReturnDate.Value.ToString("dd/MM/yyyy") : ""),
                        Csv(l.Status)
                    }));
                }
                try
                {
                    System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show("Đã xuất " + _view.Count + " phiếu.", "Xuất CSV",
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
