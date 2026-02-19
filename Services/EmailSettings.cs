namespace Vocentra.Services
{
    public class EmailSettings
    {
        public string Host { get; set; }
        public int Port { get; set; } = 25;
        public bool UseSsl { get; set; } = false;
        public string UserName { get; set; }
        public string Password { get; set; }
        public string From { get; set; }
    }
}
