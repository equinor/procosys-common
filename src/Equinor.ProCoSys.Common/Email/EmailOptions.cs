﻿namespace Equinor.ProCoSys.Common.Email
{
    public class EmailOptions
    {
        public bool Enabled { get; set; }
        public string From { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string MailUserOid { get; set; }

    }
}
