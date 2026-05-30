using PackagingInspectionTools.Core.Gpu;
using PackagingInspectionTools.Core.Network;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PackagingInspectionTools.UI
{
    public sealed class GpuControl : UserControl
    {
        private readonly IGpuService _gpuService;
        private readonly BindingSource _gpuSource = new BindingSource();
        private readonly DataGridView _gpuGrid = new DataGridView();
        private readonly NumericUpDown _powerLimitInput = new NumericUpDown();
        private readonly NumericUpDown _minClockInput = new NumericUpDown();
        private readonly NumericUpDown _maxClockInput = new NumericUpDown();
        private readonly ComboBox _computeModeComboBox = new ComboBox();
        private readonly Label _statusLabel = new Label();
        private readonly Timer _refreshTimer = new Timer();

        public GpuControl(IGpuService gpuService)
        {
            _gpuService = gpuService;
            Dock = DockStyle.Fill;
            BackColor = UiStyles.WindowBackColor;
            BuildLayout();

            _refreshTimer.Interval = 3000;
            _refreshTimer.Tick += (sender, args) => RefreshGpus(false);
            Load += (sender, args) =>
            {
                RefreshGpus(true);
                _refreshTimer.Start();
            };
            Disposed += (sender, args) => _refreshTimer.Dispose();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12),
                BackColor = UiStyles.WindowBackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            root.Controls.Add(BuildToolbar(), 0, 0);
            ConfigureGpuGrid();
            root.Controls.Add(WrapWithTitle("GPU 监控", _gpuGrid), 0, 1);
            root.Controls.Add(BuildSettingsPanel(), 0, 2);

            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.ForeColor = Color.FromArgb(70, 70, 70);
            root.Controls.Add(_statusLabel, 0, 3);
        }

        private Control BuildToolbar()
        {
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiStyles.WindowBackColor
            };

            toolbar.Controls.Add(CreateButton("刷新 GPU 状态", () => RefreshGpus(true)));
            toolbar.Controls.Add(CreateButton("启用持久模式", EnablePersistenceMode));
            toolbar.Controls.Add(CreateButton("恢复默认频率", ResetGraphicsClock));
            toolbar.Controls.Add(CreateButton("切换 TCC 模式", SetTccMode));
            toolbar.Controls.Add(CreateButton("切回 WDDM 模式", SetWddmMode));
            toolbar.Controls.Add(CreateButton("关闭 PCIe 省电", DisablePciePowerSaving));

            var note = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0),
                Text = "锁频、功耗上限和计算模式依赖 NVIDIA nvidia-smi，通常需要管理员权限。用于减少 AI 推理进程因频率波动导致的耗时抖动。",
                ForeColor = UiStyles.SecondaryTextColor
            };
            toolbar.Controls.Add(note);

            return toolbar;
        }

        private Control BuildSettingsPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 2,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = UiStyles.WindowBackColor
            };
            for (var index = 0; index < 6; index++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6));
            }
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            panel.Controls.Add(UiStyles.CreateLabeledField("功耗上限 W", Number(_powerLimitInput, 1, 1000, 200)), 0, 0);
            panel.Controls.Add(UiStyles.CreateLabeledField("最低核心频率 MHz", Number(_minClockInput, 1, 5000, 1200)), 1, 0);
            panel.Controls.Add(UiStyles.CreateLabeledField("最高核心频率 MHz", Number(_maxClockInput, 1, 5000, 1800)), 2, 0);

            _computeModeComboBox.Dock = DockStyle.Fill;
            _computeModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _computeModeComboBox.Items.AddRange(new object[] { "DEFAULT", "EXCLUSIVE_PROCESS", "PROHIBITED" });
            _computeModeComboBox.SelectedIndex = 0;
            panel.Controls.Add(UiStyles.CreateLabeledField("计算模式", _computeModeComboBox), 3, 0);

            panel.Controls.Add(CreateButton("设置功耗上限", SetPowerLimit), 0, 1);
            panel.Controls.Add(CreateButton("锁定核心频率", LockGraphicsClock), 1, 1);
            panel.Controls.Add(CreateButton("设置计算模式", SetComputeMode), 2, 1);

            return panel;
        }

        private void ConfigureGpuGrid()
        {
            _gpuGrid.Dock = DockStyle.Fill;
            _gpuGrid.AutoGenerateColumns = false;
            _gpuGrid.ReadOnly = true;
            _gpuGrid.AllowUserToAddRows = false;
            _gpuGrid.AllowUserToDeleteRows = false;
            _gpuGrid.AllowUserToResizeRows = false;
            _gpuGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gpuGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gpuGrid.MultiSelect = false;
            _gpuGrid.RowHeadersVisible = false;
            _gpuGrid.BackgroundColor = SystemColors.Window;
            _gpuGrid.BorderStyle = BorderStyle.FixedSingle;
            UiStyles.StyleGrid(_gpuGrid);
            _gpuGrid.DataSource = _gpuSource;
            _gpuGrid.Columns.Add(FillColumn("Index", "GPU", 50, 6));
            _gpuGrid.Columns.Add(FillColumn("Name", "名称", 180, 20));
            _gpuGrid.Columns.Add(FillColumn("DriverVersion", "驱动", 110, 10));
            _gpuGrid.Columns.Add(FillColumn("Utilization", "利用率", 80, 8));
            _gpuGrid.Columns.Add(FillColumn("MemoryUsed", "已用显存", 90, 9));
            _gpuGrid.Columns.Add(FillColumn("MemoryTotal", "总显存", 90, 9));
            _gpuGrid.Columns.Add(FillColumn("Temperature", "温度", 70, 7));
            _gpuGrid.Columns.Add(FillColumn("PowerDraw", "功耗", 80, 8));
            _gpuGrid.Columns.Add(FillColumn("PowerLimit", "功耗上限", 90, 9));
            _gpuGrid.Columns.Add(FillColumn("GraphicsClock", "核心频率", 90, 9));
            _gpuGrid.Columns.Add(FillColumn("MemoryClock", "显存频率", 90, 9));
            _gpuGrid.Columns.Add(FillColumn("ComputeMode", "计算模式", 130, 12));
            _gpuGrid.Columns.Add(FillColumn("Source", "来源", 70, 6));
        }

        private void RefreshGpus(bool showStatus)
        {
            try
            {
                var selectedIndex = GetSelectedGpuIndex();
                _gpuSource.DataSource = _gpuService.GetGpus().ToList();
                RestoreGpuSelection(selectedIndex);
                if (showStatus)
                {
                    _statusLabel.Text = "GPU 状态已刷新。";
                }
            }
            catch (Exception ex)
            {
                ShowError("刷新 GPU 状态失败", ex.Message);
            }
        }

        private void EnablePersistenceMode()
        {
            var gpu = RequireSelectedGpu();
            if (gpu == null)
            {
                return;
            }

            ReportResult(_gpuService.EnablePersistenceMode(gpu.Index));
            RefreshGpus(true);
        }

        private void SetPowerLimit()
        {
            var gpu = RequireSelectedGpu();
            if (gpu == null)
            {
                return;
            }

            ReportResult(_gpuService.SetPowerLimit(gpu.Index, (int)_powerLimitInput.Value));
            RefreshGpus(true);
        }

        private void LockGraphicsClock()
        {
            var gpu = RequireSelectedGpu();
            if (gpu == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                "锁定 GPU 核心频率可降低推理耗时抖动，但可能增加功耗和温度。请确认散热和电源余量。\n\n是否继续？",
                "确认锁定 GPU 核心频率",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ReportResult(_gpuService.LockGraphicsClock(gpu.Index, (int)_minClockInput.Value, (int)_maxClockInput.Value));
            RefreshGpus(true);
        }

        private void ResetGraphicsClock()
        {
            var gpu = RequireSelectedGpu();
            if (gpu == null)
            {
                return;
            }

            ReportResult(_gpuService.ResetGraphicsClock(gpu.Index));
            RefreshGpus(true);
        }

        private void SetComputeMode()
        {
            var gpu = RequireSelectedGpu();
            if (gpu == null)
            {
                return;
            }

            ReportResult(_gpuService.SetComputeMode(gpu.Index, _computeModeComboBox.Text));
            RefreshGpus(true);
        }

        private void SetTccMode()
        {
            var gpu = RequireSelectedGpu();
            if (gpu == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                "TCC 模式仅部分 NVIDIA 专业卡/计算卡支持。切换后该 GPU 通常不能负责 Windows 显示输出，需要另一张显卡或核显负责桌面显示，并且可能需要重启。\n\n是否继续请求切换为 TCC？",
                "确认切换 TCC 模式",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ReportResult(_gpuService.SetDriverModel(gpu.Index, true));
            RefreshGpus(true);
        }

        private void SetWddmMode()
        {
            var gpu = RequireSelectedGpu();
            if (gpu == null)
            {
                return;
            }

            ReportResult(_gpuService.SetDriverModel(gpu.Index, false));
            RefreshGpus(true);
        }

        private void DisablePciePowerSaving()
        {
            var confirm = MessageBox.Show(
                "将关闭当前电源计划中的 PCI Express 链路状态电源管理，避免 PCIe 总线进入低功耗状态。\n\n这有助于降低图像传输和 GPU 推理链路的唤醒延迟，但可能增加功耗。是否继续？",
                "确认关闭 PCIe 省电",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ReportResult(_gpuService.DisablePcieLinkStatePowerManagement());
        }

        private GpuInfo RequireSelectedGpu()
        {
            var gpu = _gpuGrid.CurrentRow == null ? null : _gpuGrid.CurrentRow.DataBoundItem as GpuInfo;
            if (gpu == null)
            {
                ShowError("未选择 GPU", "请先在 GPU 监控表格中选择一块 GPU。");
            }

            return gpu;
        }

        private string GetSelectedGpuIndex()
        {
            var gpu = _gpuGrid.CurrentRow == null ? null : _gpuGrid.CurrentRow.DataBoundItem as GpuInfo;
            return gpu == null ? null : gpu.Index;
        }

        private void RestoreGpuSelection(string gpuIndex)
        {
            if (string.IsNullOrWhiteSpace(gpuIndex))
            {
                return;
            }

            foreach (DataGridViewRow row in _gpuGrid.Rows)
            {
                var gpu = row.DataBoundItem as GpuInfo;
                if (gpu != null && gpu.Index == gpuIndex)
                {
                    row.Selected = true;
                    _gpuGrid.CurrentCell = row.Cells[0];
                    return;
                }
            }
        }

        private void ReportResult(OperationResult result)
        {
            _statusLabel.Text = result.Message;
            MessageBox.Show(
                result.Message,
                result.Succeeded ? "操作完成" : "操作失败",
                MessageBoxButtons.OK,
                result.Succeeded ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private static NumericUpDown Number(NumericUpDown input, int minimum, int maximum, int value)
        {
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.Value = value;
            input.Dock = DockStyle.Fill;
            UiStyles.StyleInput(input);
            return input;
        }

        private static Control WrapWithTitle(string title, Control content)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            panel.Controls.Add(content, 0, 1);
            return panel;
        }

        private static DataGridViewTextBoxColumn FillColumn(string propertyName, string header, int minimumWidth, int fillWeight)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = header,
                MinimumWidth = minimumWidth,
                FillWeight = fillWeight,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button
            {
                Text = text,
                Width = UiStyles.GetButtonWidth(text, SystemFonts.MessageBoxFont, 150),
                Height = 34,
                Margin = new Padding(0, 4, 8, 4)
            };
            UiStyles.StyleButton(button);
            button.Click += (sender, args) => action();
            return button;
        }

        private static void ShowError(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
