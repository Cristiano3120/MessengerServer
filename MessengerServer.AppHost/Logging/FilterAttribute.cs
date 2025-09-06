namespace MessengerServer.AppHost.Logging
{
    /// <summary>
    /// Set a filter string to replace the value with. Example: "***" for passwords.
    /// </summary>
    /// <param name="filterString"></param>
    [AttributeUsage(AttributeTargets.Property)]
    internal class FilterAttribute(string filterString) : Attribute
    {
        public string FilterString { get; init; } = filterString;
    }
}
