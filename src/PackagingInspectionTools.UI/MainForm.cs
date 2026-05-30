using PackagingInspectionTools.Core.Network;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PackagingInspectionTools.UI
{
    public sealed class MainForm : Form
    {
        private readonly INetworkAdapterService _networkService;
        private readonly BindingSource _adapterSource = new BindingSource();
        private readonly BindingSource _propertySource = new BindingSource();
        private readonly DataGridView _adapterGrid = new DataGridView();
        private readonly DataGridView _propertyGrid = new DataGridView();
        private readonly ComboBox _valueComboBox = new ComboBox();
        private readonly Label _statusLabel = new Label();
        private SplitContainer _contentSplit;

        private IReadOnlyList<NetworkAdapterInfo> _adapters = Array.Empty<NetworkAdapterInfo>();

        public MainForm()
            : this(new WindowsNetworkAdapterService())
        {
        }

        internal MainForm(INetworkAdapterService networkService)
        {
            _networkService = networkService;

            Text = "Packaging Inspection Tools - Network";
            MinimumSize = new Size(1100, 700);
            Size = new Size(1360, 780);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);

            BuildLayout();
            Load += (sender, args) => RefreshAdapters();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            root.Controls.Add(BuildToolbar(), 0, 0);
            root.Controls.Add(BuildContent(), 0, 1);
            root.Controls.Add(BuildEditor(), 0, 2);

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
                WrapContents = true
            };

            toolbar.Controls.Add(CreateButton("刷新", RefreshAdapters));
            toolbar.Controls.Add(CreateButton("应用 GigE 相机预设", ApplyPreset));
            toolbar.Controls.Add(CreateButton("重启选中网卡", RestartSelectedAdapter));
            toolbar.Controls.Add(CreateButton("导出当前配置", ExportCurrentConfiguration));
            toolbar.Controls.Add(CreateButton("导入配置到选中网卡", ImportConfigurationToSelectedAdapter));
            toolbar.Controls.Add(CreateButton("复制到其他网卡", CopySelectedAdapterToAnother));

            var note = new Label
            {
                AutoSize = true,
                Margin = new Padding(24, 10, 0, 0),
                Text = "写入参数和重启网卡需要管理员权限。修改生产网络前请确认当前连接不会中断关键设备。",
                ForeColor = Color.FromArgb(90, 90, 90)
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
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                Padding = new Padding(0, 12, 0, 0)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

            var valueLabel = new Label
            {
                Text = "参数值",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(valueLabel, 0, 0);

            _valueComboBox.Dock = DockStyle.Fill;
            _valueComboBox.DropDownStyle = ComboBoxStyle.DropDown;
            panel.Controls.Add(_valueComboBox, 1, 0);

            panel.Controls.Add(CreateButton("载入当前值", LoadSelectedPropertyValue), 2, 0);
            panel.Controls.Add(CreateButton("写入选中参数", SaveSelectedProperty), 3, 0);

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
            _adapterGrid.DataSource = _adapterSource;
            _adapterGrid.SelectionChanged += (sender, args) => ShowSelectedAdapterProperties();

            _adapterGrid.Columns.Add(FillColumn("Name", "名称", 150, 18));
            _adapterGrid.Columns.Add(FillColumn("OperationalStatus", "状态", 70, 7));
            _adapterGrid.Columns.Add(FillColumn("SpeedText", "速率", 90, 9));
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
                Width = 150,
                Height = 32,
                Margin = new Padding(0, 4, 8, 4)
            };
            button.Click += (sender, args) => action();
            return button;
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

        private static string BuildConfigurationCsv(IEnumerable<NetworkAdapterInfo> adapters)
        {
            var builder = new StringBuilder();
            builder.AppendLine("AdapterName,AdapterDescription,Status,Speed,MacAddress,RegistryPath,PropertyName,PropertyKey,CurrentValue,RawValue,SupportedValues");

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
                    propertyKey,
                    rawValue));
            }

            return rows;
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
            public ConfigurationRow(string adapterName, string propertyKey, string rawValue)
            {
                AdapterName = adapterName;
                PropertyKey = propertyKey;
                RawValue = rawValue;
            }

            public string AdapterName { get; }

            public string PropertyKey { get; }

            public string RawValue { get; }
        }
    }
}
