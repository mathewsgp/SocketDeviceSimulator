namespace SocketSimulator.Models
{
    public class Project
    {
        public string Name { get; set; } = "Untitled Project";
        public string FilePath { get; set; } = string.Empty;
        public ConnectionSettings ConnectionSettings { get; set; } = new();
        public ProtocolDefinition? Protocol { get; set; }
        public Scenario Scenario { get; set; } = new();
        public bool IsDirty { get; set; }
    }

    public class ConnectionSettings
    {
        public ConnectionMode Mode { get; set; } = ConnectionMode.Server;
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 9000;
    }

    public enum ConnectionMode
    {
        Server,
        Client
    }
}
