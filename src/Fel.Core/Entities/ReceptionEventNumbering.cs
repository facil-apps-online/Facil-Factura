using System;

namespace Fel.Core.Entities
{
    public class ReceptionEventNumbering
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;
        public string Prefix { get; set; } = "EV";
        public long NextNumber { get; set; } = 1;
    }
}
