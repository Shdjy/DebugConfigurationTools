using System.Collections.Generic;
using System.Linq;

namespace PackagingInspectionTools.Core.Network
{
    public sealed class AdapterAdvancedProperty
    {
        public AdapterAdvancedProperty(
            string key,
            string displayName,
            string currentValue,
            IReadOnlyList<NetworkSettingOption> options,
            bool isWritable)
        {
            Key = key;
            DisplayName = displayName;
            CurrentValue = currentValue;
            Options = options;
            IsWritable = isWritable;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public string CurrentValue { get; }

        public IReadOnlyList<NetworkSettingOption> Options { get; }

        public bool IsWritable { get; }

        public string CurrentDisplayValue
        {
            get
            {
                var option = Options.FirstOrDefault(item => item.Value == CurrentValue);
                return option == null ? CurrentValue ?? string.Empty : option.DisplayName;
            }
        }
    }
}
