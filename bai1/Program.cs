using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ClinicAppointmentManager
{
    // ===================== MODEL =====================
    public class Appointment
    {
        public string Id { get; set; }
        public string PatientName { get; set; }
        public string Phone { get; set; }
        public DateTime Time { get; set; }
        public string Doctor { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
    }

    // ===================== MAIN FORM =====================
    public class MainForm : Form
    {
        static readonly string[] Doctors = { "BS. Nguyễn Văn An", "BS. Trần Thị Bình", "BS. Lê Minh Châu", "BS. Phạm Quốc Dũng" };
        static readonly string[] Statuses = { "Chờ khám", "Đang khám", "Hoàn tất", "Đã hủy" };

        // Dữ liệu
        readonly List<Appointment> _all = new List<Appointment>();          // toàn bộ lịch hẹn
        readonly BindingList<Appointment> _view = new BindingList<Appointment>(); // danh sách sau lọc
        readonly BindingSource _bs = new BindingSource();
        Appointment _selected;     // lịch hẹn đang chọn
        bool _suppress;            // chặn nạp dữ liệu khi đang lọc/nạp

        // Controls nhập liệu
        TextBox txtId, txtName, txtPhone, txtReason;
        DateTimePicker dtpTime;
        ComboBox cboDoctor, cboStatus;
        Button btnAdd, btnUpdate, btnDelete, btnNew;
        ErrorProvider errors;

        // Tìm kiếm / lọc
        TextBox txtSearch;
        CheckBox chkDate;
        DateTimePicker dtpFilterDate;
        ComboBox cboFilterStatus;
        Button btnClearFilter;

        DataGridView grid;
        ToolStripStatusLabel lblCount;

        public MainForm()
        {
            Text = "Clinic Appointment Manager";
            Size = new Size(1100, 700);
            MinimumSize = new Size(900, 560);
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

            // ---- Layout chính: 3 hàng ----
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

            // ---- Khu vực nhập liệu ----
            var input = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 5
            };
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 5; i++) input.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtId = new TextBox();
            txtName = new TextBox();
            txtPhone = new TextBox();
            txtReason = new TextBox { Multiline = true, Height = 50 };
            dtpTime = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm" };
            cboDoctor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboDoctor.Items.AddRange(Doctors);
            cboStatus.Items.AddRange(Statuses);

            AddRow(input, 0, "Mã lịch hẹn:", txtId, "Họ tên BN:", txtName);
            AddRow(input, 1, "Số điện thoại:", txtPhone, "Ngày giờ hẹn:", dtpTime);
            AddRow(input, 2, "Bác sĩ:", cboDoctor, "Trạng thái:", cboStatus);

            input.Controls.Add(MakeLabel("Lý do khám:"), 0, 3);
            txtReason.Dock = DockStyle.Fill;
            txtReason.Margin = new Padding(3, 5, 3, 5);
            input.Controls.Add(txtReason, 1, 3);
            input.SetColumnSpan(txtReason, 3);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            btnNew = new Button { Text = "Làm mới", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnAdd = new Button { Text = "Thêm", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnUpdate = new Button { Text = "Cập nhật", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnDelete = new Button { Text = "Xóa", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnPanel.Controls.AddRange(new Control[] { btnNew, btnAdd, btnUpdate, btnDelete });
            input.Controls.Add(btnPanel, 1, 4);
            input.SetColumnSpan(btnPanel, 3);

            // ---- Thanh tìm kiếm / lọc ----
            var filter = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 8, 0, 4)
            };
            txtSearch = new TextBox { Width = 230 };
            chkDate = new CheckBox { Text = "Lọc ngày:", AutoSize = true, Margin = new Padding(15, 5, 3, 3) };
            dtpFilterDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Enabled = false };
            cboFilterStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            cboFilterStatus.Items.Add("Tất cả");
            cboFilterStatus.Items.AddRange(Statuses);
            cboFilterStatus.SelectedIndex = 0;
            btnClearFilter = new Button { Text = "Bỏ lọc", AutoSize = true };

            filter.Controls.Add(new Label { Text = "Tìm (tên / SĐT):", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
            filter.Controls.Add(txtSearch);
            filter.Controls.Add(chkDate);
            filter.Controls.Add(dtpFilterDate);
            filter.Controls.Add(new Label { Text = "Trạng thái:", AutoSize = true, Margin = new Padding(15, 7, 3, 3) });
            filter.Controls.Add(cboFilterStatus);
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
            grid.Columns.Add(Col("Id", "Mã LH", 70));
            grid.Columns.Add(Col("PatientName", "Bệnh nhân", 140));
            grid.Columns.Add(Col("Phone", "Số điện thoại", 100));
            var colTime = Col("Time", "Ngày giờ hẹn", 120);
            colTime.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
            grid.Columns.Add(colTime);
            grid.Columns.Add(Col("Doctor", "Bác sĩ", 130));
            grid.Columns.Add(Col("Reason", "Lý do khám", 180));
            grid.Columns.Add(Col("Status", "Trạng thái", 90));

            _bs.DataSource = _view;
            grid.DataSource = _bs;

            main.Controls.Add(input, 0, 0);
            main.Controls.Add(filter, 0, 1);
            main.Controls.Add(grid, 0, 2);

            // ---- StatusStrip ----
            var status = new StatusStrip();
            lblCount = new ToolStripStatusLabel("Số lịch hẹn: 0");
            status.Items.Add(lblCount);

            // ---- MenuStrip ----
            var menu = new MenuStrip();
            var mFile = new ToolStripMenuItem("Tệp");
            var mExport = new ToolStripMenuItem("Xuất danh sách ra CSV...", null, (s, e) => ExportCsv());
            mExport.ShortcutKeys = Keys.Control | Keys.E;
            var mExit = new ToolStripMenuItem("Thoát", null, (s, e) => Close());
            mFile.DropDownItems.Add(mExport);
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add(mExit);
            menu.Items.Add(mFile);

            // Thứ tự add: Fill trước, rồi Status, Menu
            Controls.Add(main);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;
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
            DateTime d = DateTime.Today;
            _all.Add(new Appointment { Id = "LH001", PatientName = "Nguyễn Thị Hoa", Phone = "0912345678", Time = d.AddHours(8.5), Doctor = Doctors[0], Reason = "Khám tổng quát", Status = "Hoàn tất" });
            _all.Add(new Appointment { Id = "LH002", PatientName = "Trần Văn Nam", Phone = "0987654321", Time = d.AddHours(9), Doctor = Doctors[1], Reason = "Đau đầu kéo dài", Status = "Đang khám" });
            _all.Add(new Appointment { Id = "LH003", PatientName = "Lê Thu Trang", Phone = "0903111222", Time = d.AddHours(14), Doctor = Doctors[2], Reason = "Tái khám tim mạch", Status = "Chờ khám" });
            _all.Add(new Appointment { Id = "LH004", PatientName = "Phạm Đức Minh", Phone = "0336789012", Time = d.AddHours(15.5), Doctor = Doctors[3], Reason = "Đau răng", Status = "Chờ khám" });
            _all.Add(new Appointment { Id = "LH005", PatientName = "Vũ Thanh Hà", Phone = "0977123456", Time = d.AddDays(1).AddHours(10), Doctor = Doctors[0], Reason = "Siêu âm ổ bụng", Status = "Chờ khám" });
        }

        // ===================== SỰ KIỆN =====================
        void WireEvents()
        {
            grid.SelectionChanged += (s, e) => LoadSelectedRow();
            grid.CellFormatting += Grid_CellFormatting;

            btnAdd.Click += (s, e) => AddAppointment();
            btnUpdate.Click += (s, e) => UpdateAppointment();
            btnDelete.Click += (s, e) => DeleteAppointment();
            btnNew.Click += (s, e) => { grid.ClearSelection(); _selected = null; ClearInputs(); };

            txtSearch.TextChanged += (s, e) => ApplyFilter();     // tìm kiếm tức thời
            chkDate.CheckedChanged += (s, e) => { dtpFilterDate.Enabled = chkDate.Checked; ApplyFilter(); };
            dtpFilterDate.ValueChanged += (s, e) => ApplyFilter();
            cboFilterStatus.SelectedIndexChanged += (s, e) => ApplyFilter();
            btnClearFilter.Click += (s, e) =>
            {
                txtSearch.Text = "";
                chkDate.Checked = false;
                cboFilterStatus.SelectedIndex = 0;
                ApplyFilter();
            };

            // Xóa lỗi khi người dùng sửa lại
            txtId.TextChanged += (s, e) => errors.SetError(txtId, "");
            txtName.TextChanged += (s, e) => errors.SetError(txtName, "");
            txtPhone.TextChanged += (s, e) => errors.SetError(txtPhone, "");
            txtReason.TextChanged += (s, e) => errors.SetError(txtReason, "");
            dtpTime.ValueChanged += (s, e) => errors.SetError(dtpTime, "");
        }

        void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _view.Count) return;
            string st = _view[e.RowIndex].Status;
            if (st == "Đã hủy") e.CellStyle.ForeColor = Color.Gray;
            else if (st == "Hoàn tất") e.CellStyle.ForeColor = Color.DarkGreen;
            else if (st == "Đang khám") e.CellStyle.ForeColor = Color.DarkOrange;
        }

        // ===================== LỌC / TÌM KIẾM =====================
        void ApplyFilter()
        {
            string kw = (txtSearch.Text ?? "").Trim();
            string kwDigits = new string(kw.Where(char.IsDigit).ToArray());
            string statusFilter = cboFilterStatus.SelectedIndex > 0 ? cboFilterStatus.SelectedItem.ToString() : null;

            IEnumerable<Appointment> q = _all;

            if (kw.Length > 0)
            {
                q = q.Where(a =>
                    a.PatientName.IndexOf(kw, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    a.Phone.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (kwDigits.Length > 0 && a.Phone.Contains(kwDigits)));
            }
            if (chkDate.Checked)
                q = q.Where(a => a.Time.Date == dtpFilterDate.Value.Date);
            if (statusFilter != null)
                q = q.Where(a => a.Status == statusFilter);

            var result = q.OrderBy(a => a.Time).ToList();

            _suppress = true;
            try
            {
                _view.RaiseListChangedEvents = false;
                _view.Clear();
                foreach (var a in result) _view.Add(a);
                _view.RaiseListChangedEvents = true;
                _view.ResetBindings();
                grid.ClearSelection();

                // Giữ lại dòng đang chọn nếu vẫn còn trong danh sách lọc
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
            lblCount.Text = string.Format("Số lịch hẹn sau lọc: {0} / Tổng: {1}", _view.Count, _all.Count);
        }

        // ===================== NẠP DỮ LIỆU LÊN FORM =====================
        void LoadSelectedRow()
        {
            if (_suppress) return;
            if (grid.CurrentRow == null || !grid.CurrentRow.Selected) return;
            var a = grid.CurrentRow.DataBoundItem as Appointment;
            if (a == null) return;

            _selected = a;
            errors.Clear();
            txtId.Text = a.Id;
            txtName.Text = a.PatientName;
            txtPhone.Text = a.Phone;
            txtReason.Text = a.Reason;
            cboDoctor.SelectedItem = a.Doctor;
            cboStatus.SelectedItem = a.Status;
            dtpTime.Value = a.Time < dtpTime.MinDate ? dtpTime.MinDate : a.Time;
        }

        void ClearInputs()
        {
            errors.Clear();
            txtId.Text = NextId();
            txtName.Text = "";
            txtPhone.Text = "";
            txtReason.Text = "";
            DateTime t = DateTime.Now.AddHours(1);
            dtpTime.Value = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0);
            cboDoctor.SelectedIndex = 0;
            cboStatus.SelectedIndex = 0;
            txtName.Focus();
        }

        string NextId()
        {
            int max = 0;
            foreach (var a in _all)
            {
                var m = Regex.Match(a.Id, @"^LH(\d+)$", RegexOptions.IgnoreCase);
                if (m.Success) max = Math.Max(max, int.Parse(m.Groups[1].Value));
            }
            return "LH" + (max + 1).ToString("000");
        }

        // ===================== KIỂM TRA DỮ LIỆU =====================
        static readonly Regex PhoneRx = new Regex(@"^(0|\+84)\d{9}$");

        static string NormalizePhone(string p)
        {
            return Regex.Replace(p ?? "", @"[\s\.\-]", "");
        }

        static DateTime TrimToMinute(DateTime d)
        {
            return new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0);
        }

        bool ValidateInputs(bool isUpdate)
        {
            errors.Clear();
            bool ok = true;

            string id = txtId.Text.Trim();
            if (id.Length == 0)
            {
                errors.SetError(txtId, "Mã lịch hẹn không được để trống");
                ok = false;
            }
            else if (_all.Any(a => !ReferenceEquals(a, isUpdate ? _selected : null) &&
                                   string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                errors.SetError(txtId, "Mã lịch hẹn đã tồn tại");
                ok = false;
            }

            if (txtName.Text.Trim().Length == 0)
            {
                errors.SetError(txtName, "Vui lòng nhập họ tên bệnh nhân");
                ok = false;
            }

            string phone = NormalizePhone(txtPhone.Text);
            if (phone.Length == 0)
            {
                errors.SetError(txtPhone, "Vui lòng nhập số điện thoại");
                ok = false;
            }
            else if (!PhoneRx.IsMatch(phone))
            {
                errors.SetError(txtPhone, "SĐT không hợp lệ (VD: 0912345678 hoặc +84912345678)");
                ok = false;
            }

            if (txtReason.Text.Trim().Length == 0)
            {
                errors.SetError(txtReason, "Vui lòng nhập lý do khám");
                ok = false;
            }

            // Ngày hẹn không nằm trong quá khứ.
            // Khi cập nhật mà KHÔNG đổi ngày giờ (chỉ đổi trạng thái...) thì không kiểm tra lại.
            bool dateUnchanged = isUpdate && _selected != null &&
                                 TrimToMinute(_selected.Time) == TrimToMinute(dtpTime.Value);
            if (!dateUnchanged && TrimToMinute(dtpTime.Value) < TrimToMinute(DateTime.Now))
            {
                errors.SetError(dtpTime, "Ngày giờ hẹn không được nằm trong quá khứ");
                ok = false;
            }

            if (cboDoctor.SelectedIndex < 0) { errors.SetError(cboDoctor, "Chọn bác sĩ"); ok = false; }
            if (cboStatus.SelectedIndex < 0) { errors.SetError(cboStatus, "Chọn trạng thái"); ok = false; }

            return ok;
        }

        // ===================== THÊM / SỬA / XÓA =====================
        void AddAppointment()
        {
            if (!ValidateInputs(false)) return;

            var a = new Appointment
            {
                Id = txtId.Text.Trim(),
                PatientName = txtName.Text.Trim(),
                Phone = NormalizePhone(txtPhone.Text),
                Time = TrimToMinute(dtpTime.Value),
                Doctor = cboDoctor.SelectedItem.ToString(),
                Reason = txtReason.Text.Trim(),
                Status = cboStatus.SelectedItem.ToString()
            };

            if (MessageBox.Show("Xác nhận thêm lịch hẹn " + a.Id + "?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _all.Add(a);
            _selected = a;
            ApplyFilter();
        }

        void UpdateAppointment()
        {
            if (_selected == null)
            {
                MessageBox.Show("Hãy chọn một lịch hẹn trong danh sách để cập nhật.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ValidateInputs(true)) return;

            if (MessageBox.Show("Xác nhận cập nhật lịch hẹn " + _selected.Id + "?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _selected.Id = txtId.Text.Trim();
            _selected.PatientName = txtName.Text.Trim();
            _selected.Phone = NormalizePhone(txtPhone.Text);
            _selected.Time = TrimToMinute(dtpTime.Value);
            _selected.Doctor = cboDoctor.SelectedItem.ToString();
            _selected.Reason = txtReason.Text.Trim();
            _selected.Status = cboStatus.SelectedItem.ToString();

            ApplyFilter();   // làm mới bảng + StatusStrip theo bộ lọc hiện tại
        }

        void DeleteAppointment()
        {
            if (_selected == null)
            {
                MessageBox.Show("Hãy chọn một lịch hẹn trong danh sách để xóa.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Bạn có chắc muốn xóa lịch hẹn " + _selected.Id + " - " + _selected.PatientName + "?",
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
                dlg.FileName = "lich_hen_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("Mã lịch hẹn,Họ tên,Số điện thoại,Ngày giờ hẹn,Bác sĩ,Lý do khám,Trạng thái");
                foreach (var a in _view)   // chỉ xuất danh sách đang hiển thị
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        Csv(a.Id), Csv(a.PatientName), Csv(a.Phone),
                        Csv(a.Time.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
                        Csv(a.Doctor), Csv(a.Reason), Csv(a.Status)
                    }));
                }
                try
                {
                    System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); // BOM để Excel đọc đúng tiếng Việt
                    MessageBox.Show("Đã xuất " + _view.Count + " lịch hẹn.", "Xuất CSV",
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
            s = s ?? "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
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
