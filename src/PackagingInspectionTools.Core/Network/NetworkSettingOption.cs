namespace PackagingInspectionTools.Core.Network
{
    public sealed class NetworkSettingOption
    {
        public NetworkSettingOption(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public string Value { get; }

        public string DisplayName { get; }
    }
}
