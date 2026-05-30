using PackagingInspectionTools.Core.Cpu;
using PackagingInspectionTools.Core.Network;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PackagingInspectionTools.UI
{
    public sealed class CpuControl : UserControl
    {
        private readonly ICpuService _cpuService;
        private readonly BindingSource _coreSource = new BindingSource();
        private readonly BindingSource _processSource = new BindingSource();
        private readonly DataGridView _coreGrid = new DataGridView();
        private readonly DataGridView _processGrid = new DataGridView();
        private readonly Label _summaryLabel = new Label();
        private readonly Label _statusLabel = new Label();
        private readonly Timer _refreshTimer = new Timer();
        private SplitContainer _cpuSplit;
        private bool _processAutoRefreshPaused;

        public CpuControl(ICpuService cpuService)
        {
            _cpuService = cpuService;
            Dock = DockStyle.Fill;
            BackColor = UiStyles.WindowBackColor;
            BuildLayout();

            _refreshTimer.Interval = 3000;
            _refreshTimer.Tick += (sender, args) => RefreshCpuView(false);
            Load += (sender, args) =>
            {
                RefreshCpuView(true);
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            root.Controls.Add(BuildToolbar(), 0, 0);

            _summaryLabel.Dock = DockStyle.Fill;
            _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
            _summaryLabel.ForeColor = Color.FromArgb(55, 55, 55);
            _summaryLabel.BackColor = UiStyles.SurfaceBackColor;
            _summaryLabel.BorderStyle = BorderStyle.FixedSingle;
            _summaryLabel.Padding = new Padding(10, 0, 10, 0);
            root.Controls.Add(_summaryLabel, 0, 1);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6
            };
            _cpuSplit = split;
            split.HandleCreated += (sender, args) => AdjustCpuSplitter();
            split.Resize += (sender, args) => AdjustCpuSplitter();
            ConfigureCoreGrid();
            ConfigureProcessGrid();
            split.Panel1.Controls.Add(WrapWithTitle("CPU 逻辑处理器", _coreGrid));
            split.Panel2.Controls.Add(WrapWithTitle("进程 CPU 设置", _processGrid));
            root.Controls.Add(split, 0, 2);

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

            toolbar.Controls.Add(CreateButton("刷新 CPU 状态", () => RefreshCpuView(true)));
            toolbar.Controls.Add(CreateButton("启用高性能电源计划", ApplyHighPerformancePowerPlan));
            toolbar.Controls.Add(CreateButton("应用低延迟 CPU 参数", ApplyLowLatencyPowerSettings));
            toolbar.Controls.Add(CreateButton("选中进程高优先级", SetSelectedProcessHighPriority));
            toolbar.Controls.Add(CreateButton("选中进程禁用小核", RestrictSelectedProcessToPerformanceCores));
            toolbar.Controls.Add(CreateButton("选中进程恢复全核心", RestoreSelectedProcessToAllCores));

            var note = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0),
                Text = "“禁用小核”会把选中进程的 CPU 亲和性限制到高性能核心，适合算法进程和低延迟通信进程；部分操作需要管理员权限。",
                ForeColor = UiStyles.SecondaryTextColor
            };
            toolbar.Controls.Add(note);

            return toolbar;
        }

        private void ConfigureCoreGrid()
        {
            _coreGrid.Dock = DockStyle.Fill;
            _coreGrid.AutoGenerateColumns = false;
            _coreGrid.ReadOnly = true;
            _coreGrid.AllowUserToAddRows = false;
            _coreGrid.AllowUserToDeleteRows = false;
            _coreGrid.AllowUserToResizeRows = false;
            _coreGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _coreGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _coreGrid.MultiSelect = false;
            _coreGrid.RowHeadersVisible = false;
            _coreGrid.BackgroundColor = SystemColors.Window;
            _coreGrid.BorderStyle = BorderStyle.FixedSingle;
            UiStyles.StyleGrid(_coreGrid);
            _coreGrid.DataSource = _coreSource;
            _coreGrid.Columns.Add(FillColumn("LogicalProcessorIndex", "逻辑 CPU", 80, 16));
            _coreGrid.Columns.Add(FillColumn("CoreTypeText", "核心类型", 120, 32));
            _coreGrid.Columns.Add(FillColumn("EfficiencyClass", "效率等级", 80, 18));
            _coreGrid.Columns.Add(FillColumn("AffinityMaskText", "亲和性掩码", 120, 34));
        }

        private void ConfigureProcessGrid()
        {
            _processGrid.Dock = DockStyle.Fill;
            _processGrid.AutoGenerateColumns = false;
            _processGrid.ReadOnly = true;
            _processGrid.AllowUserToAddRows = false;
            _processGrid.AllowUserToDeleteRows = false;
            _processGrid.AllowUserToResizeRows = false;
            _processGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _processGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _processGrid.MultiSelect = false;
            _processGrid.RowHeadersVisible = false;
            _processGrid.BackgroundColor = SystemColors.Window;
            _processGrid.BorderStyle = BorderStyle.FixedSingle;
            UiStyles.StyleGrid(_processGrid);
            _processGrid.DataSource = _processSource;
            _processGrid.MouseDown += ProcessGridMouseDown;
            _processGrid.Columns.Add(FillColumn("Id", "PID", 70, 8));
            _processGrid.Columns.Add(FillColumn("Name", "进程名", 150, 22));
            _processGrid.Columns.Add(FillColumn("WindowTitle", "标题", 220, 28));
            _processGrid.Columns.Add(FillColumn("PriorityClass", "优先级", 90, 14));
            _processGrid.Columns.Add(FillColumn("AffinityMask", "亲和性", 120, 18));
            _processGrid.Columns.Add(FillColumn("CpuTime", "CPU 时间", 100, 10));
            _processGrid.Columns.Add(FillColumn("WorkingSet", "内存", 100, 10));
        }

        private void RefreshCpuView(bool showStatus)
        {
            try
            {
                var selectedProcessId = GetSelectedProcessId();
                var firstDisplayedProcessRow = GetFirstDisplayedProcessRowIndex();
                var summary = _cpuService.GetSummary();
                _summaryLabel.Text =
                    $"CPU：{summary.ProcessorName}\r\n" +
                    $"逻辑处理器：{summary.LogicalProcessorCount}    混合架构：{(summary.HasHybridCores ? "是" : "否")}    当前电源计划：{summary.ActivePowerScheme}";

                _coreSource.DataSource = summary.Cores.ToList();
                if (showStatus || !_processAutoRefreshPaused)
                {
                    _processSource.DataSource = _cpuService.GetProcesses().ToList();
                    RestoreProcessSelection(selectedProcessId, showStatus);
                    if (!showStatus)
                    {
                        RestoreFirstDisplayedProcessRow(firstDisplayedProcessRow);
                    }
                }

                if (showStatus)
                {
                    _statusLabel.Text = "CPU 状态已刷新。";
                }
            }
            catch (Exception ex)
            {
                ShowError("刷新 CPU 状态失败", ex.Message);
            }
        }

        private void ProcessGridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _processAutoRefreshPaused = true;
                _statusLabel.Text = "进程列表自动刷新已暂停。右键点击进程列表可恢复刷新。";
            }
            else if (e.Button == MouseButtons.Right)
            {
                _processAutoRefreshPaused = false;
                _statusLabel.Text = "进程列表自动刷新已恢复。";
                RefreshCpuView(true);
            }
        }

        private void ApplyHighPerformancePowerPlan()
        {
            ReportResult(_cpuService.SetHighPerformancePowerPlan());
            RefreshCpuView(true);
        }

        private void ApplyLowLatencyPowerSettings()
        {
            var confirm = MessageBox.Show(
                "将设置当前电源计划的 CPU 参数：处理器最小/最大状态 100%、主动散热、关闭核心停车、性能偏好最高。\n\n是否继续？",
                "确认应用低延迟 CPU 参数",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ReportResult(_cpuService.ApplyLowLatencyPowerSettings());
            RefreshCpuView(true);
        }

        private void SetSelectedProcessHighPriority()
        {
            var processId = RequireSelectedProcessId();
            if (processId <= 0)
            {
                return;
            }

            ReportResult(_cpuService.SetProcessHighPriority(processId));
            RefreshCpuView(true);
        }

        private void RestrictSelectedProcessToPerformanceCores()
        {
            var processId = RequireSelectedProcessId();
            if (processId <= 0)
            {
                return;
            }

            var confirm = MessageBox.Show(
                "将把选中进程限制到高性能核心运行，用于降低算法和通信进程被调度到小核的风险。\n\n是否继续？",
                "确认禁用小核",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ReportResult(_cpuService.RestrictProcessToPerformanceCores(processId));
            RefreshCpuView(true);
        }

        private void RestoreSelectedProcessToAllCores()
        {
            var processId = RequireSelectedProcessId();
            if (processId <= 0)
            {
                return;
            }

            ReportResult(_cpuService.RestoreProcessToAllCores(processId));
            RefreshCpuView(true);
        }

        private int RequireSelectedProcessId()
        {
            var processId = GetSelectedProcessId();
            if (processId <= 0)
            {
                ShowError("未选择进程", "请先在进程列表中选择算法或通信进程。");
            }

            return processId;
        }

        private int GetSelectedProcessId()
        {
            var process = _processGrid.CurrentRow == null
                ? null
                : _processGrid.CurrentRow.DataBoundItem as ProcessCpuInfo;
            return process == null ? 0 : process.Id;
        }

        private int GetFirstDisplayedProcessRowIndex()
        {
            try
            {
                return _processGrid.Rows.Count == 0 ? -1 : _processGrid.FirstDisplayedScrollingRowIndex;
            }
            catch
            {
                return -1;
            }
        }

        private void RestoreFirstDisplayedProcessRow(int rowIndex)
        {
            if (rowIndex < 0 || _processGrid.Rows.Count == 0)
            {
                return;
            }

            try
            {
                _processGrid.FirstDisplayedScrollingRowIndex = Math.Min(rowIndex, _processGrid.Rows.Count - 1);
            }
            catch
            {
                // Ignore transient grid states during binding refresh.
            }
        }

        private void RestoreProcessSelection(int processId, bool moveCurrentCell)
        {
            if (processId <= 0)
            {
                return;
            }

            foreach (DataGridViewRow row in _processGrid.Rows)
            {
                var process = row.DataBoundItem as ProcessCpuInfo;
                if (process != null && process.Id == processId)
                {
                    row.Selected = true;
                    if (moveCurrentCell)
                    {
                        _processGrid.CurrentCell = row.Cells[0];
                    }
                    return;
                }
            }
        }

        private void AdjustCpuSplitter()
        {
            if (_cpuSplit == null || _cpuSplit.Width <= 0)
            {
                return;
            }

            var width = _cpuSplit.Width;
            if (width <= 360)
            {
                return;
            }

            var panel1Min = Math.Min(260, Math.Max(80, width / 5));
            var panel2Min = Math.Min(620, Math.Max(120, width / 2));
            if (panel1Min + panel2Min >= width)
            {
                panel2Min = Math.Max(80, width - panel1Min - 20);
            }

            var preferredDistance = Math.Max(280, (int)(_cpuSplit.Width * 0.26));
            preferredDistance = Math.Min(preferredDistance, 340);
            var minDistance = panel1Min;
            var maxDistance = Math.Max(minDistance, _cpuSplit.Width - panel2Min);
            var safeDistance = Math.Max(minDistance, Math.Min(preferredDistance, maxDistance));

            _cpuSplit.Panel1MinSize = 25;
            _cpuSplit.Panel2MinSize = 25;
            _cpuSplit.SplitterDistance = safeDistance;
            _cpuSplit.Panel1MinSize = panel1Min;
            _cpuSplit.Panel2MinSize = panel2Min;
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
                Width = UiStyles.GetButtonWidth(text, SystemFonts.MessageBoxFont, 170),
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
