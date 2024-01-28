using System;
using System.Collections.Generic;

namespace Config.config
{
    public class MainConfig
    {
        internal object webhook;

        public int prixMinimum { get; set; }
        public int prixMaximum { get; set; }
        public int temps { get; set; }

        public string Webhook { get; set; }

        public string messageNotif { get; set; }
        public string titreNotif { get; set; }

        public string smsNom { get; set; }



    }
}