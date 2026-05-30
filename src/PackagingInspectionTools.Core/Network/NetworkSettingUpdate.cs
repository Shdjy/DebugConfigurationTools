namespace PackagingInspectionTools.Core.Network
{
    public sealed class NetworkSettingUpdate
    {
        public NetworkSettingUpdate(string adapterId, string propertyKey, string value)
        {
            AdapterId = adapterId;
            PropertyKey = propertyKey;
            Value = value;
        }

        public string AdapterId { get; }

        public string PropertyKey { get; }

        public string Value { get; }
    }
}
