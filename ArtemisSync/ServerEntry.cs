namespace ArtemisSync
{
    public class ServerEntry
    {
        string _alias = "";
        string _ipAddress = "";
        string _authKey = "";
        long _utcJoinTime = 0;

        public string Alias { get => _alias; set => _alias = value; }
        public string IpAddress { get => _ipAddress; set => _ipAddress = value; }
        public string AuthKey { get => _authKey; set => _authKey = value; }
        public long UtcJoinTime { get => _utcJoinTime; set => _utcJoinTime = value; }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(_alias) ? _alias : _ipAddress;
        }
    }
}
