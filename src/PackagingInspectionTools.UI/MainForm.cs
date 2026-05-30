using PackagingInspectionTools.Core.Cpu;
using PackagingInspectionTools.Core.Gpu;
using PackagingInspectionTools.Core.Network;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PackagingInspectionTools.UI
{
    public sealed class MainForm : Form
    {
        private readonly INetworkAdapterService _networkService;
        private readonly ICpuService _cpuService;
        private readonly IGpuService _gpuService;
        private readonly BindingSource _adapterSource = new BindingSource();
        private readonly BindingSource _propertySource = new BindingSource();
        private readonly DataGridView _adapterGrid = new DataGridView();
        private readonly DataGridView _propertyGrid = new DataGridView();
        private readonly ComboBox _valueComboBox = new ComboBox();
        private readonly TextBox _ipAddressTextBox = new TextBox();
        private readonly TextBox _subnetMaskTextBox = new TextBox();
        private readonly TextBox _pingTargetTextBox = new TextBox();
        private readonly NumericUpDown _pingCountInput = new NumericUpDown();
        private readonly NumericUpDown _pingTimeoutInput = new NumericUpDown();
        private readonly NumericUpDown _pingBufferSizeInput = new NumericUpDown();
        private readonly NumericUpDown _pingTtlInput = new NumericUpDown();
        private readonly CheckBox _pingDontFragmentCheckBox = new CheckBox();
        private readonly Label _statusLabel = new Label();
        private SplitContainer _contentSplit;

        private IReadOnlyList<NetworkAdapterInfo> _adapters = Array.Empty<NetworkAdapterInfo>();

        public MainForm()
            : this(new WindowsNetworkAdapterService(), new WindowsCpuService(), new WindowsGpuService())
        {
        }

        internal MainForm(INetworkAdapterService networkService, ICpuService cpuService, IGpuService gpuService)
        {
            _networkService = networkService;
            _cpuService = cpuService;
            _gpuService = gpuService;

            Text = "Packaging Inspection Tools";
            MinimumSize = new Size(1100, 700);
            Size = new Size(1360, 780);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = UiStyles.WindowBackColor;
            Icon = new Icon(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources\\app.ico"));

            BuildLayout();
            Load += (sender, args) => RefreshAdapters();
        }

        private void BuildLayout()
        {
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };
            tabs.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            Controls.Add(tabs);

            var networkPage = new TabPage("网络设置");
            var cpuPage = new TabPage("CPU 设置");
            var gpuPage = new TabPage("GPU 设置");
            UiStyles.ApplyPage(networkPage);
            UiStyles.ApplyPage(cpuPage);
            UiStyles.ApplyPage(gpuPage);
            tabs.TabPages.Add(networkPage);
            tabs.TabPages.Add(cpuPage);
            tabs.TabPages.Add(gpuPage);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 204));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            networkPage.Controls.Add(root);

            root.Controls.Add(BuildToolbar(), 0, 0);
            root.Controls.Add(BuildContent(), 0, 1);
            root.Controls.Add(BuildEditor(), 0, 2);

            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.ForeColor = Color.FromArgb(70, 70, 70);
            root.Controls.Add(_statusLabel, 0, 3);

            cpuPage.Controls.Add(new CpuControl(_cpuService));
            gpuPage.Controls.Add(new GpuControl(_gpuService));
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

            toolbar.Controls.Add(CreateButton("刷新", RefreshAdapters));
            toolbar.Controls.Add(CreateButton("应用 GigE 相机预设", ApplyPreset));
            toolbar.Controls.Add(CreateButton("重启选中网卡", RestartSelectedAdapter));
            toolbar.Controls.Add(CreateButton("导出当前配置", ExportCurrentConfiguration));
            toolbar.Controls.Add(CreateButton("导入配置到选中网卡", ImportConfigurationToSelectedAdapter));
            toolbar.Controls.Add(CreateButton("复制到其他网卡", CopySelectedAdapterToAnother));
            toolbar.Controls.Add(CreateButton("对比标准配置", CompareSelectedAdapterWithStandardConfiguration));

            var note = new Label
            {
                AutoSize = true,
                Margin = new Padding(24, 10, 0, 0),
                Text = "写入参数和重启网卡需要管理员权限。修改生产网络前请确认当前连接不会中断关键设备。",
                ForeColor = UiStyles.SecondaryTextColor
            };
            toolbar.Controls.Add(note);

            return toolbar;
        }

        private Control BuildContent()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6
            };
            _contentSplit = split;
            split.HandleCreated += (sender, args) => AdjustSplitter();
            split.Resize += (sender, args) => AdjustSplitter();

            ConfigureAdapterGrid();
            ConfigurePropertyGrid();

            split.Panel1.Controls.Add(WrapWithTitle("网络适配器", _adapterGrid));
            split.Panel2.Controls.Add(WrapWithTitle("高级驱动参数", _propertyGrid));

            return split;
        }

        private Control BuildEditor()
        {
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = UiStyles.WindowBackColor
            };
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

            var propertyRow = CreateEditorRow(
                new ColumnStyle(SizeType.Percent, 100),
                new ColumnStyle(SizeType.Absolute, 170),
                new ColumnStyle(SizeType.Absolute, 190));
            var ipRow = CreateEditorRow(
                new ColumnStyle(SizeType.Absolute, 220),
                new ColumnStyle(SizeType.Absolute, 220),
                new ColumnStyle(SizeType.Absolute, 180),
                new ColumnStyle(SizeType.Absolute, 220),
                new ColumnStyle(SizeType.Percent, 100));
            var pingRow = CreateEditorRow(
                new ColumnStyle(SizeType.Absolute, 220),
                new ColumnStyle(SizeType.Absolute, 112),
                new ColumnStyle(SizeType.Absolute, 142),
                new ColumnStyle(SizeType.Absolute, 122),
                new ColumnStyle(SizeType.Absolute, 112),
                new ColumnStyle(SizeType.Absolute, 120),
                new ColumnStyle(SizeType.Absolute, 160),
                new ColumnStyle(SizeType.Percent, 100));
            outer.Controls.Add(propertyRow, 0, 0);
            outer.Controls.Add(ipRow, 0, 1);
            outer.Controls.Add(pingRow, 0, 2);

            _valueComboBox.DropDownStyle = ComboBoxStyle.DropDown;
            propertyRow.Controls.Add(UiStyles.CreateLabeledField("参数值", _valueComboBox), 0, 0);
            propertyRow.Controls.Add(CreateEditorButton("载入当前值", LoadSelectedPropertyValue), 1, 0);
            propertyRow.Controls.Add(CreateEditorButton("写入选中参数", SaveSelectedProperty), 2, 0);

            ipRow.Controls.Add(UiStyles.CreateLabeledField("静态 IP", _ipAddressTextBox), 0, 0);
            ipRow.Controls.Add(UiStyles.CreateLabeledField("子网掩码", _subnetMaskTextBox), 1, 0);
            ipRow.Controls.Add(CreateEditorButton("应用静态 IP", ApplyStaticIPv4Address), 2, 0);
            ipRow.Controls.Add(CreateEditorButton("恢复自动获取 IP", EnableDhcpIPv4), 3, 0);

            pingRow.Controls.Add(UiStyles.CreateLabeledField("Ping 目标", _pingTargetTextBox), 0, 0);
            pingRow.Controls.Add(UiStyles.CreateLabeledField("次数", ConfigureNumber(_pingCountInput, 1, 100, 4, 56)), 1, 0);
            pingRow.Controls.Add(UiStyles.CreateLabeledField("超时 ms", ConfigureNumber(_pingTimeoutInput, 100, 60000, 1000, 72)), 2, 0);
            pingRow.Controls.Add(UiStyles.CreateLabeledField("包大小", ConfigureNumber(_pingBufferSizeInput, 0, 65500, 32, 72)), 3, 0);
            pingRow.Controls.Add(UiStyles.CreateLabeledField("TTL", ConfigureNumber(_pingTtlInput, 1, 255, 128, 56)), 4, 0);
            pingRow.Controls.Add(CreatePingFlagPanel(), 5, 0);
            pingRow.Controls.Add(CreateEditorButton("开始 Ping", PingTarget), 6, 0);

            return outer;
        }

        private Control CreatePingFlagPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiStyles.WindowBackColor,
                Padding = new Padding(0, 28, 0, 0)
            };

            _pingDontFragmentCheckBox.Text = "禁止分片";
            _pingDontFragmentCheckBox.AutoSize = true;
            _pingDontFragmentCheckBox.Margin = new Padding(0, 0, 0, 0);
            _pingDontFragmentCheckBox.ForeColor = UiStyles.SecondaryTextColor;
            panel.Controls.Add(_pingDontFragmentCheckBox);

            return panel;
        }

        private void ConfigureAdapterGrid()
        {
            _adapterGrid.Dock = DockStyle.Fill;
            _adapterGrid.AutoGenerateColumns = false;
            _adapterGrid.ReadOnly = true;
            _adapterGrid.AllowUserToAddRows = false;
            _adapterGrid.AllowUserToDeleteRows = false;
            _adapterGrid.AllowUserToResizeRows = false;
            _adapterGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _adapterGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            _adapterGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _adapterGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _adapterGrid.MultiSelect = false;
            _adapterGrid.RowHeadersVisible = false;
            _adapterGrid.BackgroundColor = SystemColors.Window;
            _adapterGrid.BorderStyle = BorderStyle.FixedSingle;
            UiStyles.StyleGrid(_adapterGrid);
            _adapterGrid.DataSource = _adapterSource;
            _adapterGrid.SelectionChanged += (sender, args) => ShowSelectedAdapterProperties();

            _adapterGrid.Columns.Add(FillColumn("Name", "名称", 150, 18));
            _adapterGrid.Columns.Add(FillColumn("OperationalStatus", "状态", 70, 7));
            _adapterGrid.Columns.Add(FillColumn("SpeedText", "速率", 90, 9));
            _adapterGrid.Columns.Add(FillColumn("IPv4Address", "IPv4", 120, 13));
            _adapterGrid.Columns.Add(FillColumn("IPv4SubnetMask", "子网掩码", 120, 13));
            _adapterGrid.Columns.Add(FillColumn("DriverVersion", "驱动版本", 120, 13));
            _adapterGrid.Columns.Add(FillColumn("AdapterType", "类型", 110, 10));
            _adapterGrid.Columns.Add(FillColumn("MacAddress", "MAC", 125, 12));
            _adapterGrid.Columns.Add(FillColumn("Description", "描述", 260, 30));
        }

        private void ConfigurePropertyGrid()
        {
            _propertyGrid.Dock = DockStyle.Fill;
            _propertyGrid.AutoGenerateColumns = false;
            _propertyGrid.ReadOnly = true;
            _propertyGrid.AllowUserToAddRows = false;
            _propertyGrid.AllowUserToDeleteRows = false;
            _propertyGrid.AllowUserToResizeRows = false;
            _propertyGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _propertyGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            _propertyGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _propertyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _propertyGrid.MultiSelect = false;
            _propertyGrid.RowHeadersVisible = false;
            _propertyGrid.BackgroundColor = SystemColors.Window;
            _propertyGrid.BorderStyle = BorderStyle.FixedSingle;
            UiStyles.StyleGrid(_propertyGrid);
            _propertyGrid.DataSource = _propertySource;
            _propertyGrid.SelectionChanged += (sender, args) => LoadSelectedPropertyValue();

            _propertyGrid.Columns.Add(FillColumn("DisplayName", "参数", 220, 35));
            _propertyGrid.Columns.Add(FillColumn("CurrentDisplayValue", "当前值", 220, 40));
            _propertyGrid.Columns.Add(FillColumn("Key", "驱动键", 140, 25));
        }

        private void RefreshAdapters()
        {
            try
            {
                _statusLabel.Text = "正在读取网络适配器...";
                _adapters = _networkService.GetAdapters();
                _adapterSource.DataSource = _adapters.ToList();
                ShowSelectedAdapterProperties();
                _statusLabel.Text = $"已读取 {_adapters.Count} 个网络适配器。";
            }
            catch (Exception ex)
            {
                ShowError("读取网卡失败", ex.Message);
            }
        }

        private void ShowSelectedAdapterProperties()
        {
            var adapter = GetSelectedAdapter();
            _propertySource.DataSource = adapter == null
                ? new List<AdapterAdvancedProperty>()
                : adapter.AdvancedProperties.ToList();
            _ipAddressTextBox.Text = adapter == null ? string.Empty : adapter.IPv4Address;
            _subnetMaskTextBox.Text = adapter == null ? string.Empty : adapter.IPv4SubnetMask;
            LoadSelectedPropertyValue();
        }

        private void LoadSelectedPropertyValue()
        {
            var property = GetSelectedProperty();
            _valueComboBox.Items.Clear();

            if (property == null)
            {
                _valueComboBox.Text = string.Empty;
                return;
            }

            foreach (var option in property.Options)
            {
                _valueComboBox.Items.Add(new ComboBoxOption(option.DisplayName, option.Value));
            }

            var selected = _valueComboBox.Items
                .OfType<ComboBoxOption>()
                .FirstOrDefault(item => item.Value == property.CurrentValue);
            if (selected != null)
            {
                _valueComboBox.SelectedItem = selected;
            }
            else
            {
                _valueComboBox.Text = property.CurrentValue ?? string.Empty;
            }
        }

        private void SaveSelectedProperty()
        {
            var adapter = GetSelectedAdapter();
            var property = GetSelectedProperty();
            if (adapter == null || property == null)
            {
                ShowError("无法写入", "请先选择网卡和参数。");
                return;
            }

            var value = GetSelectedValue();
            if (string.IsNullOrWhiteSpace(value))
            {
                ShowError("无法写入", "参数值不能为空。");
                return;
            }

            var confirm = MessageBox.Show(
                $"即将修改网卡“{adapter.Name}”的参数：{property.DisplayName}\n\n新值：{value}\n\n是否继续？",
                "确认写入网络参数",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            var result = _networkService.SetAdvancedProperty(new NetworkSettingUpdate(adapter.Id, property.Key, value));
            ReportResult(result);
            RefreshAdapters();
        }

        private void ApplyPreset()
        {
            var adapter = GetSelectedAdapter();
            if (adapter == null)
            {
                ShowError("无法应用预设", "请先选择网卡。");
                return;
            }

            var confirm = MessageBox.Show(
                $"将尝试为“{adapter.Name}”写入 GigE 相机常用高吞吐参数。\n\n该操作只会修改驱动已暴露且能匹配到的参数。是否继续？",
                "确认应用预设",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            var result = _networkService.ApplyGigECameraPreset(adapter.Id);
            ReportResult(result);
            RefreshAdapters();
        }

        private void RestartSelectedAdapter()
        {
            var adapter = GetSelectedAdapter();
            if (adapter == null)
            {
                ShowError("无法重启", "请先选择网卡。");
                return;
            }

            var confirm = MessageBox.Show(
                $"将临时禁用并重新启用网卡“{adapter.Name}”。\n\n该操作会中断此网卡上的连接，是否继续？",
                "确认重启网卡",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ReportResult(_networkService.RestartAdapter(adapter.Name));
            RefreshAdapters();
        }

        private void ApplyStaticIPv4Address()
        {
            var adapter = GetSelectedAdapter();
            if (adapter == null)
            {
                ShowError("无法设置 IP", "请先选择网卡。");
                return;
            }

            var ipAddress = _ipAddressTextBox.Text.Trim();
            var subnetMask = _subnetMaskTextBox.Text.Trim();
            if (!IsIPv4Address(ipAddress) || !IsIPv4Address(subnetMask))
            {
                ShowError("无法设置 IP", "请输入有效的 IPv4 地址和子网掩码，例如 192.168.1.10 / 255.255.255.0。");
                return;
            }

            var confirm = MessageBox.Show(
                $"将关闭网卡“{adapter.Name}”的自动获取 IP，并设置静态地址：\n\nIP：{ipAddress}\n子网掩码：{subnetMask}\n\n该操作可能中断当前网络连接，是否继续？",
                "确认设置静态 IP",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ReportResult(_networkService.SetStaticIPv4Address(adapter.Name, ipAddress, subnetMask));
            RefreshAdapters();
        }

        private void EnableDhcpIPv4()
        {
            var adapter = GetSelectedAdapter();
            if (adapter == null)
            {
                ShowError("无法恢复自动获取 IP", "请先选择网卡。");
                return;
            }

            var confirm = MessageBox.Show(
                $"将把网卡“{adapter.Name}”恢复为自动获取 IPv4 地址。\n\n该操作可能中断当前网络连接，是否继续？",
                "确认恢复自动获取 IP",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ReportResult(_networkService.EnableDhcpIPv4(adapter.Name));
            RefreshAdapters();
        }

        private void PingTarget()
        {
            var target = _pingTargetTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(target) || target.Contains("\"") || target.Any(char.IsWhiteSpace))
            {
                ShowError("无法 Ping", "请输入有效的 IP 地址或主机名，目标中不能包含空格。");
                return;
            }

            var adapter = GetSelectedAdapter();
            var sourceAddress = adapter != null && IsIPv4Address(adapter.IPv4Address)
                ? adapter.IPv4Address
                : string.Empty;

            var request = new NetworkPingRequest(
                target,
                (int)_pingCountInput.Value,
                (int)_pingTimeoutInput.Value,
                (int)_pingBufferSizeInput.Value,
                (int)_pingTtlInput.Value,
                _pingDontFragmentCheckBox.Checked,
                sourceAddress);

            _statusLabel.Text = "正在 Ping " + target + "...";
            ShowRealtimePingDialog(
                "Ping 结果",
                "目标：" + target + Environment.NewLine +
                "源地址：" + (string.IsNullOrWhiteSpace(sourceAddress) ? "系统自动选择" : sourceAddress) + Environment.NewLine + Environment.NewLine +
                "正在执行 Ping..." + Environment.NewLine,
                request);
        }

        private void ExportCurrentConfiguration()
        {
            var adapter = GetSelectedAdapter();
            if (adapter == null)
            {
                ShowError("无法导出", "请先选择要导出配置的网卡。");
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "导出当前网卡参数配置";
                dialog.Filter = "CSV 文件 (*.csv)|*.csv";
                dialog.FileName = "NetworkAdapterSettings_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    File.WriteAllText(dialog.FileName, BuildConfigurationCsv(new[] { adapter }), new UTF8Encoding(true));
                    _statusLabel.Text = "已导出选中网卡参数配置：" + adapter.Name;
                    MessageBox.Show("导出完成。", "导出当前配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    ShowError("导出失败", ex.Message);
                }
            }
        }

        private void ImportConfigurationToSelectedAdapter()
        {
            var adapter = GetSelectedAdapter();
            if (adapter == null)
            {
                ShowError("无法导入", "请先选择要写入配置的目标网卡。");
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择已导出的网卡配置";
                dialog.Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                List<ConfigurationRow> rows;
                try
                {
                    rows = ReadConfigurationRows(dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowError("导入失败", ex.Message);
                    return;
                }

                if (rows.Count == 0)
                {
                    ShowError("导入失败", "配置文件中没有可写入的网卡参数。");
                    return;
                }

                var sourceNames = rows
                    .Select(item => item.AdapterName)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item)
                    .ToList();

                var selectedSource = sourceNames.Count <= 1
                    ? sourceNames.FirstOrDefault()
                    : SelectValue("选择导入来源", "配置文件中包含多个网卡，请选择要导入的来源配置。", sourceNames);
                if (sourceNames.Count > 1 && selectedSource == null)
                {
                    return;
                }

                var settings = rows
                    .Where(item => selectedSource == null || string.Equals(item.AdapterName, selectedSource, StringComparison.OrdinalIgnoreCase))
                    .Select(item => new PropertyValue(item.PropertyKey, item.RawValue))
                    .ToList();

                ConfirmAndApplySettings(
                    "确认导入配置",
                    $"将把配置文件中的 {settings.Count} 个参数尝试写入目标网卡“{adapter.Name}”。\n\n只会写入目标网卡已支持的参数，是否继续？",
                    adapter,
                    settings);
            }
        }

        private void CopySelectedAdapterToAnother()
        {
            var sourceAdapter = GetSelectedAdapter();
            if (sourceAdapter == null)
            {
                ShowError("无法复制", "请先选择作为来源的网卡。");
                return;
            }

            var targetNames = _adapters
                .Where(item => !string.Equals(item.Id, sourceAdapter.Id, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Name)
                .OrderBy(item => item)
                .ToList();
            if (targetNames.Count == 0)
            {
                ShowError("无法复制", "没有其他可作为目标的网卡。");
                return;
            }

            var selectedTargetName = SelectValue("选择目标网卡", "选择要写入当前网卡参数的目标网卡。", targetNames);
            if (selectedTargetName == null)
            {
                return;
            }

            var targetAdapter = _adapters.First(item => item.Name == selectedTargetName);
            var settings = sourceAdapter.AdvancedProperties
                .Where(item => !string.IsNullOrWhiteSpace(item.CurrentValue))
                .Select(item => new PropertyValue(item.Key, item.CurrentValue))
                .ToList();

            ConfirmAndApplySettings(
                "确认复制参数",
                $"将把网卡“{sourceAdapter.Name}”的 {settings.Count} 个参数尝试写入目标网卡“{targetAdapter.Name}”。\n\n只会写入目标网卡已支持的参数，是否继续？",
                targetAdapter,
                settings);
        }

        private void CompareSelectedAdapterWithStandardConfiguration()
        {
            var adapter = GetSelectedAdapter();
            if (adapter == null)
            {
                ShowError("无法对比", "请先选择要对比的当前网卡。");
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择标准网卡配置";
                dialog.Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                List<ConfigurationRow> rows;
                try
                {
                    rows = ReadConfigurationRows(dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowError("对比失败", ex.Message);
                    return;
                }

                if (rows.Count == 0)
                {
                    ShowError("对比失败", "标准配置文件中没有可对比的网卡参数。");
                    return;
                }

                var sourceNames = rows
                    .Select(item => item.AdapterName)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item)
                    .ToList();

                var selectedSource = sourceNames.Count <= 1
                    ? sourceNames.FirstOrDefault()
                    : SelectValue("选择标准配置", "标准配置文件中包含多个网卡，请选择用于对比的配置。", sourceNames);
                if (sourceNames.Count > 1 && selectedSource == null)
                {
                    return;
                }

                var standardRows = rows
                    .Where(item => selectedSource == null || string.Equals(item.AdapterName, selectedSource, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var comparisonRows = BuildComparisonRows(adapter, standardRows);
                ShowComparisonDialog(adapter.Name, selectedSource, comparisonRows);
            }
        }

        private void ConfirmAndApplySettings(string title, string message, NetworkAdapterInfo targetAdapter, IList<PropertyValue> settings)
        {
            if (settings.Count == 0)
            {
                ShowError(title, "没有可写入的参数。");
                return;
            }

            var confirm = MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            var result = ApplySettingsToAdapter(targetAdapter, settings);
            ReportResult(result);
            RefreshAdapters();
        }

        private OperationResult ApplySettingsToAdapter(NetworkAdapterInfo targetAdapter, IEnumerable<PropertyValue> settings)
        {
            var targetProperties = targetAdapter.AdvancedProperties.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
            var applied = 0;
            var skipped = 0;
            var failures = new List<string>();

            foreach (var setting in settings)
            {
                AdapterAdvancedProperty targetProperty;
                if (!targetProperties.TryGetValue(setting.PropertyKey, out targetProperty) || string.IsNullOrWhiteSpace(setting.Value))
                {
                    skipped++;
                    continue;
                }

                if (targetProperty.Options.Count > 0 && targetProperty.Options.All(item => item.Value != setting.Value))
                {
                    skipped++;
                    continue;
                }

                var writeResult = _networkService.SetAdvancedProperty(new NetworkSettingUpdate(targetAdapter.Id, targetProperty.Key, setting.Value));
                if (writeResult.Succeeded)
                {
                    applied++;
                }
                else
                {
                    failures.Add(targetProperty.DisplayName + ": " + writeResult.Message);
                }
            }

            if (failures.Count > 0)
            {
                return OperationResult.Failure($"成功写入 {applied} 项，跳过 {skipped} 项，失败 {failures.Count} 项。\n\n" + string.Join(Environment.NewLine, failures));
            }

            return OperationResult.Success($"成功写入 {applied} 项，跳过 {skipped} 项。请重启目标网卡或重启 Windows 使设置生效。");
        }

        private NetworkAdapterInfo GetSelectedAdapter()
        {
            return _adapterGrid.CurrentRow?.DataBoundItem as NetworkAdapterInfo;
        }

        private AdapterAdvancedProperty GetSelectedProperty()
        {
            return _propertyGrid.CurrentRow?.DataBoundItem as AdapterAdvancedProperty;
        }

        private string GetSelectedValue()
        {
            return _valueComboBox.SelectedItem is ComboBoxOption option
                ? option.Value
                : _valueComboBox.Text.Trim();
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

        private void AdjustSplitter()
        {
            if (_contentSplit == null || _contentSplit.Width <= 0)
            {
                return;
            }

            var preferredDistance = Math.Max(460, (int)(_contentSplit.Width * 0.46));
            var maxDistance = Math.Max(1, _contentSplit.Width - 520);
            _contentSplit.SplitterDistance = Math.Min(preferredDistance, maxDistance);
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

        private static TableLayoutPanel CreateEditorRow(params ColumnStyle[] columns)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columns.Length,
                RowCount = 1,
                BackColor = UiStyles.WindowBackColor
            };

            foreach (var column in columns)
            {
                row.ColumnStyles.Add(column);
            }

            return row;
        }

        private static Button CreateEditorButton(string text, Action action)
        {
            var button = CreateButton(text, action);
            button.Margin = new Padding(0, 24, 8, 4);
            button.Height = 34;
            return button;
        }

        private static Label CreateInlineLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(8, 9, 2, 0)
            };
        }

        private static NumericUpDown ConfigureNumber(NumericUpDown input, int minimum, int maximum, int value, int width)
        {
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.Value = value;
            input.Width = width;
            input.Margin = new Padding(0, 5, 0, 0);
            return input;
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

        private static void ShowError(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void ShowTextDialog(string title, string text)
        {
            using (var form = new Form())
            using (var layout = new TableLayoutPanel())
            using (var textBox = new TextBox())
            using (var closeButton = new Button())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new Size(780, 520);
                form.Font = new Font("Microsoft YaHei UI", 9F);

                layout.Dock = DockStyle.Fill;
                layout.Padding = new Padding(12);
                layout.RowCount = 2;
                layout.ColumnCount = 1;
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                form.Controls.Add(layout);

                textBox.Dock = DockStyle.Fill;
                textBox.Multiline = true;
                textBox.ReadOnly = true;
                textBox.ScrollBars = ScrollBars.Both;
                textBox.WordWrap = false;
                textBox.Font = new Font("Consolas", 9F);
                textBox.Text = text;
                layout.Controls.Add(textBox, 0, 0);

                closeButton.Text = "关闭";
                closeButton.Width = 96;
                closeButton.Height = 32;
                closeButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
                closeButton.DialogResult = DialogResult.OK;
                layout.Controls.Add(closeButton, 0, 1);
                form.AcceptButton = closeButton;
                form.ShowDialog();
            }
        }

        private void ShowRealtimePingDialog(string title, string initialText, NetworkPingRequest request)
        {
            using (var form = new Form())
            using (var layout = new TableLayoutPanel())
            using (var textBox = new TextBox())
            using (var closeButton = new Button())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new Size(780, 520);
                form.Font = new Font("Microsoft YaHei UI", 9F);

                layout.Dock = DockStyle.Fill;
                layout.Padding = new Padding(12);
                layout.RowCount = 2;
                layout.ColumnCount = 1;
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                form.Controls.Add(layout);

                textBox.Dock = DockStyle.Fill;
                textBox.Multiline = true;
                textBox.ReadOnly = true;
                textBox.ScrollBars = ScrollBars.Both;
                textBox.WordWrap = false;
                textBox.Font = new Font("Consolas", 9F);
                textBox.Text = initialText;
                layout.Controls.Add(textBox, 0, 0);

                closeButton.Text = "关闭";
                closeButton.Width = 96;
                closeButton.Height = 32;
                closeButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
                closeButton.Enabled = false;
                closeButton.DialogResult = DialogResult.OK;
                layout.Controls.Add(closeButton, 0, 1);
                form.AcceptButton = closeButton;

                form.Shown += (sender, args) =>
                {
                    Task.Factory.StartNew(() =>
                    {
                        var result = _networkService.Ping(request, text =>
                        {
                            AppendTextSafe(textBox, text);
                        });

                        AppendTextSafe(textBox, Environment.NewLine + result.Message + Environment.NewLine);
                        SetPingDialogCompleted(form, closeButton, result);
                    });
                };

                form.ShowDialog(this);
            }
        }

        private void AppendTextSafe(TextBox textBox, string text)
        {
            if (textBox.IsDisposed)
            {
                return;
            }

            if (textBox.InvokeRequired)
            {
                textBox.BeginInvoke(new Action(() => AppendTextSafe(textBox, text)));
                return;
            }

            textBox.AppendText(text);
            textBox.SelectionStart = textBox.TextLength;
            textBox.ScrollToCaret();
        }

        private void SetPingDialogCompleted(Form form, Button closeButton, OperationResult result)
        {
            if (form.IsDisposed)
            {
                return;
            }

            if (form.InvokeRequired)
            {
                form.BeginInvoke(new Action(() => SetPingDialogCompleted(form, closeButton, result)));
                return;
            }

            closeButton.Enabled = true;
            _statusLabel.Text = result.Succeeded ? "Ping 完成。" : "Ping 失败。";
        }

        private static string BuildConfigurationCsv(IEnumerable<NetworkAdapterInfo> adapters)
        {
            var builder = new StringBuilder();
            builder.AppendLine("AdapterName,AdapterDescription,Status,Speed,MacAddress,IPv4Address,IPv4SubnetMask,DriverVersion,RegistryPath,PropertyName,PropertyKey,CurrentValue,RawValue,SupportedValues");

            foreach (var adapter in adapters)
            {
                if (adapter.AdvancedProperties.Count == 0)
                {
                    AppendCsvRow(builder, adapter, null);
                    continue;
                }

                foreach (var property in adapter.AdvancedProperties)
                {
                    AppendCsvRow(builder, adapter, property);
                }
            }

            return builder.ToString();
        }

        private static List<ConfigurationRow> ReadConfigurationRows(string fileName)
        {
            var lines = File.ReadAllLines(fileName, Encoding.UTF8);
            var rows = new List<ConfigurationRow>();
            if (lines.Length <= 1)
            {
                return rows;
            }

            var headers = ParseCsvLine(lines[0]);
            var adapterNameIndex = headers.FindIndex(item => string.Equals(item, "AdapterName", StringComparison.OrdinalIgnoreCase));
            var ipv4AddressIndex = headers.FindIndex(item => string.Equals(item, "IPv4Address", StringComparison.OrdinalIgnoreCase));
            var ipv4SubnetMaskIndex = headers.FindIndex(item => string.Equals(item, "IPv4SubnetMask", StringComparison.OrdinalIgnoreCase));
            var propertyKeyIndex = headers.FindIndex(item => string.Equals(item, "PropertyKey", StringComparison.OrdinalIgnoreCase));
            var rawValueIndex = headers.FindIndex(item => string.Equals(item, "RawValue", StringComparison.OrdinalIgnoreCase));

            if (adapterNameIndex < 0 || propertyKeyIndex < 0 || rawValueIndex < 0)
            {
                throw new InvalidDataException("配置文件格式不正确，缺少 AdapterName、PropertyKey 或 RawValue 列。");
            }

            for (var i = 1; i < lines.Length; i++)
            {
                var fields = ParseCsvLine(lines[i]);
                var propertyKey = GetField(fields, propertyKeyIndex);
                var rawValue = GetField(fields, rawValueIndex);
                if (string.IsNullOrWhiteSpace(propertyKey) || string.IsNullOrWhiteSpace(rawValue))
                {
                    continue;
                }

                rows.Add(new ConfigurationRow(
                    GetField(fields, adapterNameIndex),
                    GetField(fields, ipv4AddressIndex),
                    GetField(fields, ipv4SubnetMaskIndex),
                    propertyKey,
                    rawValue));
            }

            return rows;
        }

        private static List<ComparisonRow> BuildComparisonRows(NetworkAdapterInfo adapter, IEnumerable<ConfigurationRow> standardRows)
        {
            var result = new List<ComparisonRow>();
            var currentProperties = adapter.AdvancedProperties.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
            var standardNetwork = standardRows.FirstOrDefault();
            var standardProperties = standardRows
                .GroupBy(item => item.PropertyKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(item => item.Key, item => item.First(), StringComparer.OrdinalIgnoreCase);

            if (standardNetwork != null && !string.IsNullOrWhiteSpace(standardNetwork.IPv4Address))
            {
                result.Add(new ComparisonRow(
                    string.Equals(adapter.IPv4Address, standardNetwork.IPv4Address, StringComparison.OrdinalIgnoreCase) ? "一致" : "不同",
                    "IPv4Address",
                    adapter.IPv4Address,
                    standardNetwork.IPv4Address,
                    "IPv4 地址"));
            }

            if (standardNetwork != null && !string.IsNullOrWhiteSpace(standardNetwork.IPv4SubnetMask))
            {
                result.Add(new ComparisonRow(
                    string.Equals(adapter.IPv4SubnetMask, standardNetwork.IPv4SubnetMask, StringComparison.OrdinalIgnoreCase) ? "一致" : "不同",
                    "IPv4SubnetMask",
                    adapter.IPv4SubnetMask,
                    standardNetwork.IPv4SubnetMask,
                    "IPv4 子网掩码"));
            }

            foreach (var standard in standardProperties.Values.OrderBy(item => item.PropertyKey))
            {
                AdapterAdvancedProperty current;
                if (!currentProperties.TryGetValue(standard.PropertyKey, out current))
                {
                    result.Add(new ComparisonRow(
                        "当前网卡缺失",
                        standard.PropertyKey,
                        string.Empty,
                        standard.RawValue,
                        "当前网卡驱动未暴露该参数"));
                    continue;
                }

                var currentValue = current.CurrentValue ?? string.Empty;
                var status = string.Equals(currentValue, standard.RawValue, StringComparison.OrdinalIgnoreCase)
                    ? "一致"
                    : "不同";
                result.Add(new ComparisonRow(
                    status,
                    standard.PropertyKey,
                    current.CurrentDisplayValue,
                    standard.RawValue,
                    current.DisplayName));
            }

            foreach (var current in currentProperties.Values.OrderBy(item => item.Key))
            {
                if (standardProperties.ContainsKey(current.Key))
                {
                    continue;
                }

                result.Add(new ComparisonRow(
                    "标准配置未包含",
                    current.Key,
                    current.CurrentDisplayValue,
                    string.Empty,
                    current.DisplayName));
            }

            return result;
        }

        private static void ShowComparisonDialog(string adapterName, string standardName, IList<ComparisonRow> rows)
        {
            using (var form = new Form())
            using (var root = new TableLayoutPanel())
            using (var summaryLabel = new Label())
            using (var grid = new DataGridView())
            using (var closeButton = new Button())
            {
                var sameCount = rows.Count(item => item.Status == "一致");
                var differentCount = rows.Count(item => item.Status == "不同");
                var missingCount = rows.Count(item => item.Status == "当前网卡缺失");
                var extraCount = rows.Count(item => item.Status == "标准配置未包含");

                form.Text = "网卡配置对比";
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = true;
                form.ClientSize = new Size(980, 640);
                form.Font = new Font("Microsoft YaHei UI", 9F);

                root.Dock = DockStyle.Fill;
                root.Padding = new Padding(12);
                root.RowCount = 3;
                root.ColumnCount = 1;
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                form.Controls.Add(root);

                summaryLabel.Dock = DockStyle.Fill;
                summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
                summaryLabel.Text =
                    $"当前网卡：{adapterName}\r\n" +
                    $"标准配置：{(string.IsNullOrWhiteSpace(standardName) ? "配置文件" : standardName)}    一致：{sameCount}    不同：{differentCount}    缺失：{missingCount}    额外：{extraCount}";
                root.Controls.Add(summaryLabel, 0, 0);

                grid.Dock = DockStyle.Fill;
                grid.AutoGenerateColumns = false;
                grid.ReadOnly = true;
                grid.AllowUserToAddRows = false;
                grid.AllowUserToDeleteRows = false;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                grid.MultiSelect = false;
                grid.RowHeadersVisible = false;
                grid.BackgroundColor = SystemColors.Window;
                grid.BorderStyle = BorderStyle.FixedSingle;
                grid.Columns.Add(FillColumn("Status", "结果", 110, 12));
                grid.Columns.Add(FillColumn("PropertyKey", "驱动键", 150, 18));
                grid.Columns.Add(FillColumn("CurrentValue", "当前值", 220, 30));
                grid.Columns.Add(FillColumn("StandardValue", "标准值", 220, 30));
                grid.Columns.Add(FillColumn("Description", "说明", 220, 30));
                grid.DataSource = rows.ToList();
                grid.CellFormatting += (sender, args) =>
                {
                    if (args.RowIndex < 0)
                    {
                        return;
                    }

                    var row = grid.Rows[args.RowIndex].DataBoundItem as ComparisonRow;
                    if (row == null)
                    {
                        return;
                    }

                    if (row.Status == "一致")
                    {
                        grid.Rows[args.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(235, 248, 238);
                    }
                    else if (row.Status == "不同")
                    {
                        grid.Rows[args.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 244, 220);
                    }
                    else
                    {
                        grid.Rows[args.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);
                    }
                };
                root.Controls.Add(grid, 0, 1);

                closeButton.Text = "关闭";
                closeButton.Width = 96;
                closeButton.Height = 32;
                closeButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
                closeButton.DialogResult = DialogResult.OK;
                root.Controls.Add(closeButton, 0, 2);
                form.AcceptButton = closeButton;

                form.ShowDialog();
            }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var currentChar = line[i];
                if (currentChar == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (currentChar == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(currentChar);
                }
            }

            values.Add(current.ToString());
            return values;
        }

        private static string GetField(IList<string> fields, int index)
        {
            return index >= 0 && index < fields.Count ? fields[index] : string.Empty;
        }

        private static string SelectValue(string title, string message, IList<string> values)
        {
            using (var form = new Form())
            using (var layout = new TableLayoutPanel())
            using (var label = new Label())
            using (var listBox = new ListBox())
            using (var buttons = new FlowLayoutPanel())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.ClientSize = new Size(460, 300);
                form.Font = new Font("Microsoft YaHei UI", 9F);

                layout.Dock = DockStyle.Fill;
                layout.Padding = new Padding(12);
                layout.RowCount = 3;
                layout.ColumnCount = 1;
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                form.Controls.Add(layout);

                label.Text = message;
                label.Dock = DockStyle.Fill;
                label.TextAlign = ContentAlignment.MiddleLeft;
                layout.Controls.Add(label, 0, 0);

                listBox.Dock = DockStyle.Fill;
                foreach (var value in values)
                {
                    listBox.Items.Add(value);
                }
                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }
                layout.Controls.Add(listBox, 0, 1);

                buttons.Dock = DockStyle.Fill;
                buttons.FlowDirection = FlowDirection.RightToLeft;
                okButton.Text = "确定";
                okButton.Width = 88;
                okButton.DialogResult = DialogResult.OK;
                cancelButton.Text = "取消";
                cancelButton.Width = 88;
                cancelButton.DialogResult = DialogResult.Cancel;
                buttons.Controls.Add(cancelButton);
                buttons.Controls.Add(okButton);
                layout.Controls.Add(buttons, 0, 2);

                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                return form.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null
                    ? listBox.SelectedItem.ToString()
                    : null;
            }
        }

        private static void AppendCsvRow(StringBuilder builder, NetworkAdapterInfo adapter, AdapterAdvancedProperty property)
        {
            var supportedValues = property == null
                ? string.Empty
                : string.Join(" | ", property.Options.Select(item => item.DisplayName + "=" + item.Value));

            var fields = new[]
            {
                adapter.Name,
                adapter.Description,
                adapter.OperationalStatus,
                adapter.SpeedText,
                adapter.MacAddress,
                adapter.IPv4Address,
                adapter.IPv4SubnetMask,
                adapter.DriverVersion,
                adapter.RegistryPath,
                property == null ? string.Empty : property.DisplayName,
                property == null ? string.Empty : property.Key,
                property == null ? string.Empty : property.CurrentDisplayValue,
                property == null ? string.Empty : property.CurrentValue,
                supportedValues
            };

            builder.AppendLine(string.Join(",", fields.Select(CsvEscape)));
        }

        private static string CsvEscape(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static bool IsIPv4Address(string value)
        {
            IPAddress address;
            return IPAddress.TryParse(value, out address) && address.AddressFamily == AddressFamily.InterNetwork;
        }

        private sealed class ComboBoxOption
        {
            public ComboBoxOption(string displayName, string value)
            {
                DisplayName = displayName;
                Value = value;
            }

            public string DisplayName { get; }

            public string Value { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class PropertyValue
        {
            public PropertyValue(string propertyKey, string value)
            {
                PropertyKey = propertyKey;
                Value = value;
            }

            public string PropertyKey { get; }

            public string Value { get; }
        }

        private sealed class ConfigurationRow
        {
            public ConfigurationRow(string adapterName, string ipv4Address, string ipv4SubnetMask, string propertyKey, string rawValue)
            {
                AdapterName = adapterName;
                IPv4Address = ipv4Address;
                IPv4SubnetMask = ipv4SubnetMask;
                PropertyKey = propertyKey;
                RawValue = rawValue;
            }

            public string AdapterName { get; }

            public string IPv4Address { get; }

            public string IPv4SubnetMask { get; }

            public string PropertyKey { get; }

            public string RawValue { get; }
        }

        private sealed class ComparisonRow
        {
            public ComparisonRow(string status, string propertyKey, string currentValue, string standardValue, string description)
            {
                Status = status;
                PropertyKey = propertyKey;
                CurrentValue = currentValue;
                StandardValue = standardValue;
                Description = description;
            }

            public string Status { get; }

            public string PropertyKey { get; }

            public string CurrentValue { get; }

            public string StandardValue { get; }

            public string Description { get; }
        }
    }
}
