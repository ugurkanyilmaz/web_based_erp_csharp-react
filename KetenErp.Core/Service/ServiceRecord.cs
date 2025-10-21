using System;
using System.Collections.Generic;

namespace KetenErp.Core.Service
{
    public class ServiceRecord
    {
        public int Id { get; set; }
        public string? SeriNo { get; set; }
        public string? UrunModeli { get; set; }
        public string? FirmaIsmi { get; set; }
        public DateTime GelisTarihi { get; set; }
        public string? BelgeNo { get; set; }
        public string? AlanKisi { get; set; }

        public ICollection<ServiceOperation> Operations { get; set; } = new List<ServiceOperation>();
    }
}
