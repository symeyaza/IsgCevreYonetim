namespace IsgCevreYonetim.Domain.Entities
{
    /// <summary>
    /// Kısa ömürlü heartbeat kaydı. Eski kayıtlar son görülme zamanına göre çevrimiçi sayılmaz.
    /// </summary>
    public sealed class OnlineKullaniciOturumu
    {
        public string SessionKey { get; set; } = string.Empty;
        public int PersonelId { get; set; }
        public int CompanyId { get; set; }
        public int BranchId { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}
