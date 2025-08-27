namespace ArtemisSync
{
    public class ServerEntry
    {
        string _alias = "";
        string _ipAddress = "";
        string _authKey = "";

        public string Alias { get => _alias; set => _alias = value; }
        public string IpAddress { get => _ipAddress; set => _ipAddress = value; }
        public string AuthKey { get => _authKey; set => _authKey = value; }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(_alias) ? _alias : _ipAddress;
        }
    }
}
