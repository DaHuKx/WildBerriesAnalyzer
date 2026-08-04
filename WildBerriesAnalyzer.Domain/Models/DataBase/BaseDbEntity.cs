using System;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    public class BaseDbEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
