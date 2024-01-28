using Life.Network;
using SQLite;
using System;

namespace MonPV
{
    internal class Historique
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }

        public int character { get; set; }
        public DateTime historique { get; set; }
        public string prix { get; set; }
        public string plaque { get; set; }
    }
}