using IsgCevreYonetim.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Personel> Personeller { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<Gorev> Gorevler { get; set; }
        public DbSet<Grup> Gruplar { get; set; }
        public DbSet<Il> Iller { get; set; }
        public DbSet<Ilce> Ilceler { get; set; }
        public DbSet<Cinsiyet> Cinsiyetler { get; set; }
        public DbSet<Vardiya> Vardiyalar { get; set; }
        public DbSet<MudahaleSekli> MudahaleSekilleri { get; set; }
        public DbSet<DashboardGuncelleme> DashboardGuncellemeleri { get; set; }
        public DbSet<Bildirim> Bildirimler { get; set; }
        public DbSet<OnlineKullaniciOturumu> OnlineKullaniciOturumlari { get; set; }
        public DbSet<OzelMesaj> OzelMesajlar { get; set; }
        public DbSet<MesajEki> MesajEkleri { get; set; } = null!;
        public DbSet<MesajPushAboneligi> MesajPushAbonelikleri { get; set; } = null!;
        public DbSet<MesajGrubu> MesajGruplari { get; set; } = null!;
        public DbSet<MesajGrubuUyelik> MesajGrubuUyelikleri { get; set; } = null!;
        public DbSet<MesajGrubuMesaji> MesajGrubuMesajlari { get; set; } = null!;

        public DbSet<Yetki> Yetkiler { get; set; }
        public DbSet<Sayfa> Sayfalar { get; set; }
        public DbSet<YetkiSayfa> YetkiSayfalar { get; set; }
        public DbSet<YetkiSube> YetkiSubeler { get; set; }

        // ⭐ İş Kazası DbSet'leri
        public DbSet<IsKazasi> IsKazalari { get; set; }
        public DbSet<IsKazasiDosya> IsKazasiDosyalar { get; set; }
        public DbSet<IsKazasiArastirmaKategori> IsKazasiArastirmaKategorileri { get; set; }
        public DbSet<IsKazasiArastirmaMadde> IsKazasiArastirmaMaddeleri { get; set; }
        public DbSet<IsKazasiArastirma> IsKazasiArastirmalari { get; set; }
        public DbSet<IsKazasiArastirmaCevap> IsKazasiArastirmaCevaplari { get; set; }
        public DbSet<IsKazasiDuzelticiFaaliyet> IsKazasiDuzelticiFaaliyetler { get; set; }
        public DbSet<IsKazasiDuzelticiFaaliyetDosya> IsKazasiDuzelticiFaaliyetDosyalari { get; set; }
        public DbSet<IsKazasiSahit> IsKazasiSahitleri { get; set; }

        // Tehlikeli İşler
        public DbSet<TehlikeSinifi> TehlikeSiniflari { get; set; }
        public DbSet<TehlikeSinifiMadde> TehlikeSinifiMaddeleri { get; set; }
        public DbSet<IsDurumu> IsDurumlari { get; set; }
        public DbSet<TehlikeliIs> TehlikeliIsler { get; set; }
        public DbSet<TehlikeliIsGun> TehlikeliIsGunleri { get; set; }
        public DbSet<TehlikeliIsGunMadde> TehlikeliIsGunMaddeleri { get; set; }
        public DbSet<TehlikeliIsGunTehlikeSinifi> TehlikeliIsGunTehlikeSiniflari { get; set; }
        public DbSet<TehlikeliIsKisi> TehlikeliIsKisileri { get; set; }
        public DbSet<TehlikeliIsDosya> TehlikeliIsDosyalari { get; set; }
        public DbSet<TehlikeliIsDenetimKaydi> TehlikeliIsDenetimKayitlari { get; set; }
        public DbSet<TaseronFirma> TaseronFirmalar => Set<TaseronFirma>();
        public DbSet<TaseronKisi> TaseronKisiler => Set<TaseronKisi>();

        public DbSet<AtikTuru> AtikTurleri => Set<AtikTuru>();
        public DbSet<AtikFirmaTuru> AtikFirmaTurleri => Set<AtikFirmaTuru>();
        public DbSet<AtikFirma> AtikFirmalari => Set<AtikFirma>();
        public DbSet<Atik> Atiklar => Set<Atik>();
        public DbSet<AtikTakibi> AtikTakipleri => Set<AtikTakibi>();
        public DbSet<AtikTakipDosyasi> AtikTakipDosyalari => Set<AtikTakipDosyasi>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigurePersonel(modelBuilder);
            modelBuilder.Entity<TaseronFirma>(e => {
                e.ToTable("TaseronFirmalar", t => t.ExcludeFromMigrations());
                e.HasQueryFilter(x => !x.IsDeleted);
                e.HasIndex(x => new { x.CompanyId, x.BranchId, x.FirmaAdi }).IsUnique();
            });
            modelBuilder.Entity<TaseronKisi>(e => {
                e.ToTable("TaseronKisiler", t => t.ExcludeFromMigrations());
                e.HasQueryFilter(x => !x.IsDeleted);
                e.HasIndex(x => new { x.BranchId, x.TaseronFirmaId });
                e.HasOne(x => x.TaseronFirma).WithMany(x => x.Kisiler).HasForeignKey(x => x.TaseronFirmaId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<AtikTuru>(e => { e.ToTable("AtikTurleri", t => t.ExcludeFromMigrations()); e.HasQueryFilter(x => !x.IsDeleted); e.HasIndex(x => new { x.BranchId, x.AtikTurAdi }).IsUnique(); });
            modelBuilder.Entity<AtikFirmaTuru>(e => { e.ToTable("AtikFirmaTurleri", t => t.ExcludeFromMigrations()); e.HasQueryFilter(x => !x.IsDeleted); e.HasIndex(x => new { x.BranchId, x.AtikFirmaTuruAdi }).IsUnique(); });
            modelBuilder.Entity<AtikFirma>(e => { e.ToTable("AtikFirmalari", t => t.ExcludeFromMigrations()); e.HasQueryFilter(x => !x.IsDeleted); e.HasIndex(x => new { x.BranchId, x.AtikFirmaTuruId, x.AtikFirmaAdi }).IsUnique(); e.HasOne(x => x.AtikFirmaTuru).WithMany().HasForeignKey(x => x.AtikFirmaTuruId).OnDelete(DeleteBehavior.Restrict); });
            modelBuilder.Entity<Atik>(e => { e.ToTable("Atiklar", t => t.ExcludeFromMigrations()); e.HasQueryFilter(x => !x.IsDeleted); e.HasIndex(x => new { x.BranchId, x.AtikKodu }).IsUnique(); e.HasOne(x => x.AtikTuru).WithMany().HasForeignKey(x => x.AtikTuruId).OnDelete(DeleteBehavior.Restrict); });
            modelBuilder.Entity<AtikTakibi>(e => { e.ToTable("AtikTakipleri", t => t.ExcludeFromMigrations()); e.HasQueryFilter(x => !x.IsDeleted); e.Property(x => x.Miktar).HasPrecision(18,3); e.Property(x => x.BertarafBedeli).HasPrecision(18,2); e.Property(x => x.NakliyeBedeli).HasPrecision(18,2); e.Property(x => x.HesaplananTutar).HasPrecision(18,2); e.HasIndex(x => new { x.BranchId, x.Tarih }); e.HasOne(x => x.Atik).WithMany().HasForeignKey(x => x.AtikId).OnDelete(DeleteBehavior.Restrict); e.HasOne(x => x.TasiyiciFirma).WithMany().HasForeignKey(x => x.TasiyiciFirmaId).OnDelete(DeleteBehavior.Restrict); e.HasOne(x => x.AliciFirma).WithMany().HasForeignKey(x => x.AliciFirmaId).OnDelete(DeleteBehavior.Restrict); });
            modelBuilder.Entity<AtikTakipDosyasi>(e => { e.ToTable("AtikTakipDosyalari", t => t.ExcludeFromMigrations()); e.HasQueryFilter(x => !x.IsDeleted); e.HasOne(x => x.AtikTakibi).WithMany(x => x.Dosyalar).HasForeignKey(x => x.AtikTakibiId).OnDelete(DeleteBehavior.Cascade); });


            modelBuilder.Entity<IsKazasi>()
                .HasOne(x => x.Vardiya)
                .WithMany(v => v.IsKazalari)
                .HasForeignKey(x => x.VardiyaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsKazasi>()
                .HasOne(x => x.MudahaleSekli)
                .WithMany(x => x.IsKazalari)
                .HasForeignKey(x => x.MudahaleSekliId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsKazasi>()
                .HasOne(x => x.SorumluAmirPersonel)
                .WithMany()
                .HasForeignKey(x => x.SorumluAmirPersonelId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsKazasiDuzelticiFaaliyet>()
                .HasOne(x => x.IsKazasi).WithMany(x => x.DuzelticiFaaliyetler)
                .HasForeignKey(x => x.IsKazasiId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IsKazasiDuzelticiFaaliyetDosya>()
                .HasOne(x => x.DuzelticiFaaliyet).WithMany(x => x.Dosyalar)
                .HasForeignKey(x => x.IsKazasiDuzelticiFaaliyetId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IsKazasiSahit>()
                .HasOne(x => x.IsKazasi).WithMany(x => x.Sahitler)
                .HasForeignKey(x => x.IsKazasiId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IsKazasiSahit>()
                .HasOne(x => x.Personel).WithMany()
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<IsKazasiSahit>()
                .HasIndex(x => new { x.IsKazasiId, x.PersonelId }).IsUnique();
            modelBuilder.Entity<IsKazasiSahit>().Property(x => x.AdSoyad)
                .IsRequired().HasMaxLength(200);

            // Unique Index'ler
            // ⭐ Gorev'den Kod kaldırıldı - sadece Ad üzerinden unique kontrol
            modelBuilder.Entity<Gorev>().HasIndex(g => g.Ad).IsUnique();

            // ⭐ Grup'tan Kod kaldırıldı - sadece Ad üzerinden unique kontrol
            modelBuilder.Entity<Grup>().HasIndex(g => g.Ad).IsUnique();

            modelBuilder.Entity<Il>().HasIndex(i => i.Kod).IsUnique();
            modelBuilder.Entity<Il>().HasIndex(i => i.Ad).IsUnique();
            modelBuilder.Entity<Ilce>().HasIndex(i => new { i.IlId, i.Ad }).IsUnique();

            // ⭐ Cinsiyet'ten KisaKod kaldırıldı - sadece Ad üzerinden unique kontrol
            modelBuilder.Entity<Cinsiyet>().HasIndex(c => c.Ad).IsUnique();
            modelBuilder.Entity<Vardiya>().HasIndex(v => v.VardiyaAdi).IsUnique();
            modelBuilder.Entity<MudahaleSekli>().HasIndex(x => x.Ad).IsUnique();
            modelBuilder.Entity<MudahaleSekli>().Property(x => x.Ad).IsRequired().HasMaxLength(150);
            modelBuilder.Entity<MudahaleSekli>().Property(x => x.Aciklama).HasMaxLength(500);
            modelBuilder.Entity<DashboardGuncelleme>()
                .HasIndex(x => new { x.Kategori, x.Durum, x.Sira, x.IsDeleted })
                .HasDatabaseName("IX_DashboardGuncellemeleri_Kategori_Durum_Sira_IsDeleted");

            modelBuilder.Entity<MesajGrubu>((Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<MesajGrubu> e) => { e.ToTable("MesajGruplari"); e.HasKey(x => x.Id); e.Property(x => x.Ad).HasMaxLength(120).IsRequired(); e.HasIndex(x => new { x.CompanyId, x.BranchId }); });
            modelBuilder.Entity<MesajGrubuUyelik>((Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<MesajGrubuUyelik> e) => { e.ToTable("MesajGrubuUyelikleri"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.GrupId, x.PersonelId }).IsUnique(); e.HasIndex(x => new { x.PersonelId, x.KabulUtc, x.RedUtc }); e.HasOne<MesajGrubu>().WithMany().HasForeignKey(x => x.GrupId).OnDelete(DeleteBehavior.Cascade); });
            modelBuilder.Entity<MesajGrubuMesaji>((Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<MesajGrubuMesaji> e) => { e.ToTable("MesajGrubuMesajlari"); e.HasKey(x => x.Id); e.Property(x => x.Icerik).HasMaxLength(2000).IsRequired(); e.HasIndex(x => new { x.GrupId, x.Id }); e.HasOne<MesajGrubu>().WithMany().HasForeignKey(x => x.GrupId).OnDelete(DeleteBehavior.Cascade); });
            modelBuilder.Entity<MesajEki>(e =>
            {
                e.ToTable("MesajEkleri", t => t.HasCheckConstraint("CK_MesajEkleri_TekMesaj",
                    "([OzelMesajId] IS NULL AND [GrupMesajiId] IS NOT NULL) OR ([OzelMesajId] IS NOT NULL AND [GrupMesajiId] IS NULL)"));
                e.HasKey(x => x.Id);
                e.Property(x => x.DosyaAdi).HasMaxLength(180).IsRequired();
                e.Property(x => x.DepoAdi).HasMaxLength(64).IsRequired();
                e.Property(x => x.IcerikTuru).HasMaxLength(100).IsRequired();
                e.HasIndex(x => x.OzelMesajId);
                e.HasIndex(x => x.GrupMesajiId);
                e.HasOne(x => x.OzelMesaj).WithMany().HasForeignKey(x => x.OzelMesajId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.GrupMesaji).WithMany().HasForeignKey(x => x.GrupMesajiId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<MesajPushAboneligi>(e =>
            {
                e.ToTable("MesajPushAbonelikleri");
                e.HasKey(x => x.Id);
                e.Property(x => x.Endpoint).HasMaxLength(2048).IsRequired();
                e.Property(x => x.EndpointHash).HasMaxLength(64).IsRequired();
                e.Property(x => x.P256dh).HasMaxLength(256).IsRequired();
                e.Property(x => x.Auth).HasMaxLength(256).IsRequired();
                e.HasIndex(x => x.EndpointHash).IsUnique();
                e.HasIndex(x => x.PersonelId);
            });
            modelBuilder.Entity<OzelMesaj>(entity =>
            {
                entity.ToTable("OzelMesajlar");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Icerik).IsRequired().HasMaxLength(2000);
                entity.HasIndex(x => new { x.CompanyId, x.BranchId, x.AliciPersonelId, x.OkunmaUtc });
                entity.HasIndex(x => new { x.CompanyId, x.BranchId, x.GonderenPersonelId, x.GonderimUtc });
            });

            modelBuilder.Entity<OnlineKullaniciOturumu>(entity =>
            {
                entity.ToTable("OnlineKullaniciOturumlari");
                entity.HasKey(x => x.SessionKey);
                entity.Property(x => x.SessionKey).HasMaxLength(64).IsRequired();
                entity.HasIndex(x => new { x.CompanyId, x.BranchId, x.LastSeenUtc })
                    .HasDatabaseName("IX_OnlineKullaniciOturumlari_Scope_LastSeenUtc");
                entity.HasIndex(x => x.PersonelId)
                    .HasDatabaseName("IX_OnlineKullaniciOturumlari_PersonelId");
            });

            modelBuilder.Entity<DashboardGuncelleme>().Property(x => x.SayfaAdi)
                .IsRequired().HasMaxLength(150);
            modelBuilder.Entity<DashboardGuncelleme>().Property(x => x.Aciklama)
                .HasMaxLength(500);

            modelBuilder.Entity<Company>().HasIndex(c => c.CompanyKodu).IsUnique();
            modelBuilder.Entity<Branch>().HasIndex(b => b.BranchKodu).IsUnique();
            modelBuilder.Entity<Department>().HasIndex(d => d.DepartmentKodu).IsUnique();
            modelBuilder.Entity<Unit>().HasIndex(u => u.UnitKodu).IsUnique();

            modelBuilder.Entity<Yetki>().HasIndex(y => y.Ad).IsUnique();
            modelBuilder.Entity<Sayfa>().HasIndex(s => s.Url).IsUnique();

            modelBuilder.Entity<YetkiSayfa>().HasIndex(ys => new { ys.YetkiId, ys.BranchId, ys.SayfaId }).IsUnique();
            modelBuilder.Entity<YetkiSube>().HasIndex(ys => new { ys.YetkiId, ys.BranchId }).IsUnique();

            // Performans indexleri: sık kullanılan tenant/şube ve yetki filtrelerini destekler.
            modelBuilder.Entity<Personel>()
                .HasIndex(p => new { p.CompanyId, p.BranchId, p.IsDeleted })
                .HasDatabaseName("IX_Personeller_CompanyId_BranchId_IsDeleted");
            modelBuilder.Entity<Personel>()
                .HasIndex(p => new { p.BranchId, p.AktifMi, p.IsDeleted })
                .HasDatabaseName("IX_Personeller_BranchId_AktifMi_IsDeleted");
            modelBuilder.Entity<Personel>()
                .HasIndex(p => new { p.BranchId, p.Ad, p.IsDeleted, p.AktifMi })
                .HasDatabaseName("IX_Personeller_BranchId_Ad_IsDeleted_AktifMi");
            modelBuilder.Entity<Personel>()
                .HasIndex(p => new { p.BranchId, p.Soyad, p.IsDeleted, p.AktifMi })
                .HasDatabaseName("IX_Personeller_BranchId_Soyad_IsDeleted_AktifMi");
            modelBuilder.Entity<IsKazasi>()
                .HasIndex(i => new { i.BranchId, i.IsDeleted, i.KazaTarihi })
                .HasDatabaseName("IX_IsKazalari_BranchId_IsDeleted_KazaTarihi");
            modelBuilder.Entity<IsKazasi>()
                .HasIndex(i => new { i.PersonelId, i.IsDeleted })
                .HasDatabaseName("IX_IsKazalari_PersonelId_IsDeleted");
            modelBuilder.Entity<Branch>()
                .HasIndex(b => new { b.CompanyId, b.IsDeleted, b.IsActive })
                .HasDatabaseName("IX_Branches_CompanyId_IsDeleted_IsActive");
            modelBuilder.Entity<Department>()
                .HasIndex(d => new { d.BranchId, d.IsDeleted, d.IsActive })
                .HasDatabaseName("IX_Departments_BranchId_IsDeleted_IsActive");
            modelBuilder.Entity<Unit>()
                .HasIndex(u => new { u.DepartmentId, u.IsDeleted, u.IsActive })
                .HasDatabaseName("IX_Units_DepartmentId_IsDeleted_IsActive");
            modelBuilder.Entity<YetkiSayfa>()
                .HasIndex(ys => new { ys.YetkiId, ys.BranchId, ys.IsActive, ys.IsDeleted })
                .HasDatabaseName("IX_YetkiSayfalar_YetkiId_BranchId_IsActive_IsDeleted");

            modelBuilder.Entity<IsKazasiArastirmaKategori>()
                .HasIndex(x => x.Ad)
                .IsUnique();
            modelBuilder.Entity<IsKazasiArastirmaMadde>()
                .HasIndex(x => new { x.KategoriId, x.Sira });
            modelBuilder.Entity<IsKazasiArastirma>()
                .HasIndex(x => x.IsKazasiId)
                .IsUnique();
            modelBuilder.Entity<IsKazasiArastirmaCevap>()
                .HasIndex(x => new { x.IsKazasiArastirmaId, x.MaddeId })
                .IsUnique();

            ConfigureTehlikeliIsler(modelBuilder);

            // Soft Delete - Query Filter
            modelBuilder.Entity<Personel>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Company>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Branch>().HasQueryFilter(b => !b.IsDeleted);
            modelBuilder.Entity<Department>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<Unit>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<Gorev>().HasQueryFilter(g => !g.IsDeleted);
            modelBuilder.Entity<Grup>().HasQueryFilter(g => !g.IsDeleted);
            modelBuilder.Entity<Il>().HasQueryFilter(i => !i.IsDeleted);
            modelBuilder.Entity<Ilce>().HasQueryFilter(i => !i.IsDeleted);
            modelBuilder.Entity<Cinsiyet>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Vardiya>().HasQueryFilter(v => !v.IsDeleted);
            modelBuilder.Entity<MudahaleSekli>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<DashboardGuncelleme>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<Yetki>().HasQueryFilter(y => !y.IsDeleted);
            modelBuilder.Entity<Sayfa>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<YetkiSayfa>().HasQueryFilter(ys => !ys.IsDeleted);
            modelBuilder.Entity<YetkiSube>().HasQueryFilter(ys => !ys.IsDeleted);

            // ⭐ İş Kazası Soft Delete
            modelBuilder.Entity<IsKazasi>().HasQueryFilter(i => !i.IsDeleted);
            modelBuilder.Entity<IsKazasiDosya>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<IsKazasiArastirmaKategori>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<IsKazasiArastirmaMadde>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<IsKazasiArastirma>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<IsKazasiArastirmaCevap>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<IsKazasiDuzelticiFaaliyet>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<IsKazasiDuzelticiFaaliyetDosya>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<IsKazasiSahit>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<TehlikeSinifi>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<TehlikeSinifiMadde>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<IsDurumu>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<TehlikeliIs>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<TehlikeliIsGun>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<TehlikeliIsGunMadde>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<TehlikeliIsGunTehlikeSinifi>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<TehlikeliIsKisi>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<TehlikeliIsDosya>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<TehlikeliIsDenetimKaydi>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<Bildirim>().HasQueryFilter(x => !x.IsDeleted);

            ConfigureRelationships(modelBuilder);
        }

        private static void ConfigureTehlikeliIsler(ModelBuilder modelBuilder)
        {
            // Canlı veritabanını koruyan idempotent SQL migration'ı kullanılır. Bu tabloların
            // sonraki otomatik migration'larda yanlışlıkla yeniden oluşturulmasını engelle.
            modelBuilder.Entity<TehlikeSinifi>().ToTable("TehlikeSiniflari", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<TehlikeSinifiMadde>().ToTable("TehlikeSinifiMaddeleri", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<IsDurumu>().ToTable("IsDurumlari", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<TehlikeliIs>().ToTable("TehlikeliIsler", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<TehlikeliIsGun>().ToTable("TehlikeliIsGunleri", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<TehlikeliIsGunMadde>().ToTable("TehlikeliIsGunMaddeleri", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<TehlikeliIsGunTehlikeSinifi>().ToTable("TehlikeliIsGunTehlikeSiniflari", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<TehlikeliIsKisi>().ToTable("TehlikeliIsKisileri", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<TehlikeliIsDosya>().ToTable("TehlikeliIsDosyalari", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<TehlikeliIsDenetimKaydi>().ToTable("TehlikeliIsDenetimKayitlari", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<Bildirim>().ToTable("Bildirimler", t => t.ExcludeFromMigrations());

            modelBuilder.Entity<TehlikeSinifi>().HasIndex(x => x.Ad).IsUnique();
            modelBuilder.Entity<TehlikeSinifiMadde>().HasIndex(x => new { x.TehlikeSinifiId, x.Sira });
            modelBuilder.Entity<IsDurumu>().HasIndex(x => x.Ad).IsUnique();
            modelBuilder.Entity<TehlikeliIs>().HasIndex(x => x.DogrulamaKodu).IsUnique();
            modelBuilder.Entity<TehlikeliIs>().HasIndex(x => new { x.BranchId, x.IsDeleted, x.Tarih });
            modelBuilder.Entity<TehlikeliIsGun>().HasIndex(x => new { x.TehlikeliIsId, x.GunNo }).IsUnique();
            modelBuilder.Entity<TehlikeliIsGunMadde>().HasIndex(x => new { x.TehlikeliIsGunId, x.MaddeId }).IsUnique();
            modelBuilder.Entity<TehlikeliIsGunTehlikeSinifi>().HasIndex(x => new { x.TehlikeliIsGunId, x.TehlikeSinifiId }).IsUnique();
            modelBuilder.Entity<Bildirim>().HasIndex(x => x.ReferansAnahtari).IsUnique();
            modelBuilder.Entity<Bildirim>().HasIndex(x => new { x.PersonelId, x.OkunduMu, x.CreatedDate });

            modelBuilder.Entity<TehlikeSinifiMadde>().HasOne(x => x.TehlikeSinifi).WithMany(x => x.Maddeler)
                .HasForeignKey(x => x.TehlikeSinifiId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.CalismaYapacakBirim).WithMany().HasForeignKey(x => x.CalismaYapacakBirimId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.TaseronFirma).WithMany().HasForeignKey(x => x.TaseronFirmaId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.TaseronYetkili).WithMany().HasForeignKey(x => x.TaseronYetkiliId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.TehlikeSinifi).WithMany(x => x.TehlikeliIsler).HasForeignKey(x => x.TehlikeSinifiId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.IsDurumu).WithMany(x => x.TehlikeliIsler).HasForeignKey(x => x.IsDurumuId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.IsiYaptiranPersonel).WithMany().HasForeignKey(x => x.IsiYaptiranPersonelId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.FirmaSorumlusuPersonel).WithMany().HasForeignKey(x => x.FirmaSorumlusuPersonelId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.KontrolEdenPersonel).WithMany().HasForeignKey(x => x.KontrolEdenPersonelId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIs>().HasOne(x => x.OnaylayanPersonel).WithMany().HasForeignKey(x => x.OnaylayanPersonelId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIsGun>().HasOne(x => x.TehlikeliIs).WithMany(x => x.Gunler).HasForeignKey(x => x.TehlikeliIsId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TehlikeliIsGun>().HasOne(x => x.KontrolEdenPersonel).WithMany().HasForeignKey(x => x.KontrolEdenPersonelId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIsGun>().HasOne(x => x.OnaylayanPersonel).WithMany().HasForeignKey(x => x.OnaylayanPersonelId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIsGunMadde>().HasOne(x => x.Gun).WithMany(x => x.Maddeler).HasForeignKey(x => x.TehlikeliIsGunId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TehlikeliIsGunMadde>().HasOne(x => x.Madde).WithMany(x => x.GunMaddeleri).HasForeignKey(x => x.MaddeId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIsGunTehlikeSinifi>().HasOne(x => x.Gun).WithMany(x => x.TehlikeSiniflari).HasForeignKey(x => x.TehlikeliIsGunId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TehlikeliIsGunTehlikeSinifi>().HasOne(x => x.TehlikeSinifi).WithMany().HasForeignKey(x => x.TehlikeSinifiId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIsKisi>().HasOne(x => x.TehlikeliIs).WithMany(x => x.Kisiler).HasForeignKey(x => x.TehlikeliIsId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TehlikeliIsKisi>().HasOne(x => x.Gun).WithMany(x => x.Kisiler).HasForeignKey(x => x.TehlikeliIsGunId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TehlikeliIsKisi>().HasOne(x => x.Personel).WithMany().HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TehlikeliIsKisi>().HasOne(x => x.TaseronKisi).WithMany().HasForeignKey(x => x.TaseronKisiId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TehlikeliIsKisi>().HasOne(x => x.OnaylayanPersonel).WithMany().HasForeignKey(x => x.OnaylayanPersonelId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TehlikeliIsDosya>().HasOne(x => x.TehlikeliIs).WithMany(x => x.Dosyalar).HasForeignKey(x => x.TehlikeliIsId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TehlikeliIsDosya>().HasOne(x => x.Gun).WithMany(x => x.Dosyalar).HasForeignKey(x => x.TehlikeliIsGunId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TehlikeliIsDenetimKaydi>().HasOne(x => x.TehlikeliIs).WithMany(x => x.DenetimKayitlari).HasForeignKey(x => x.TehlikeliIsId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TehlikeliIsDenetimKaydi>().HasOne(x => x.Gun).WithMany().HasForeignKey(x => x.TehlikeliIsGunId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TehlikeliIsDenetimKaydi>().HasOne(x => x.Personel).WithMany().HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Bildirim>().HasOne(x => x.Personel).WithMany().HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Cascade);
        }

        private void ConfigurePersonel(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<Personel>();

            entity.Property(p => p.Id).UseIdentityColumn(seed: 100000, increment: 1);
            entity.HasIndex(p => p.SicilNo).IsUnique();
            entity.HasIndex(p => p.Email).IsUnique();

            entity.Property(p => p.Ad).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Soyad).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Email).IsRequired().HasMaxLength(200);
            entity.Property(p => p.SicilNo).IsRequired().HasMaxLength(20);

            entity.Property(p => p.SifreHash).HasMaxLength(500);
            entity.Property(p => p.SifreSalt).HasMaxLength(500);
            entity.Property(p => p.SonGirisIpAdresi).HasMaxLength(50);
            entity.Property(p => p.SonGirisUserAgent).HasMaxLength(500);

            // MVC tarafında [Required] ile zorunlu; mevcut veritabanı geçişlerinde null kayıtlar olabileceği için DB kolonu nullable kalır.
            entity.Property(p => p.YetkiId).IsRequired(false);

            entity.HasOne(p => p.Gorev)
                .WithMany(g => g.Personeller)
                .HasForeignKey(p => p.GorevId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Grup)
                .WithMany(g => g.Personeller)
                .HasForeignKey(p => p.GrupId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Cinsiyet)
                .WithMany(c => c.Personeller)
                .HasForeignKey(p => p.CinsiyetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Yetki)
                .WithMany(y => y.Personeller)
                .HasForeignKey(p => p.YetkiId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private void ConfigureRelationships(ModelBuilder modelBuilder)
        {
            // Personel ilişkileri
            modelBuilder.Entity<Personel>()
                .HasOne(p => p.Company)
                .WithMany(c => c.Personeller)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Personel>()
                .HasOne(p => p.Branch)
                .WithMany(b => b.Personeller)
                .HasForeignKey(p => p.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Personel>()
                .HasOne(p => p.Department)
                .WithMany(d => d.Personeller)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Personel>()
                .HasOne(p => p.Unit)
                .WithMany(u => u.Personeller)
                .HasForeignKey(p => p.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // Branch ilişkileri
            modelBuilder.Entity<Branch>()
                .HasOne(b => b.Company)
                .WithMany(c => c.Branches)
                .HasForeignKey(b => b.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Branch>()
                .HasOne(b => b.Il)
                .WithMany(i => i.Branches)
                .HasForeignKey(b => b.IlId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Branch>()
                .HasOne(b => b.Ilce)
                .WithMany(i => i.Branches)
                .HasForeignKey(b => b.IlceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Department - Branch
            modelBuilder.Entity<Department>()
                .HasOne(d => d.Branch)
                .WithMany(b => b.Departments)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unit - Department
            modelBuilder.Entity<Unit>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Units)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ilce - Il
            modelBuilder.Entity<Ilce>()
                .HasOne(i => i.Il)
                .WithMany(i => i.Ilceler)
                .HasForeignKey(i => i.IlId)
                .OnDelete(DeleteBehavior.Restrict);

            // Yetkilendirme ilişkileri
            modelBuilder.Entity<YetkiSayfa>()
                .HasOne(ys => ys.Sayfa)
                .WithMany(s => s.YetkiSayfalar)
                .HasForeignKey(ys => ys.SayfaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<YetkiSayfa>()
                .HasOne(ys => ys.Yetki)
                .WithMany(y => y.YetkiSayfalar)
                .HasForeignKey(ys => ys.YetkiId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<YetkiSayfa>()
                .HasOne(ys => ys.Branch)
                .WithMany()
                .HasForeignKey(ys => ys.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<YetkiSube>()
                .HasOne(ys => ys.Yetki)
                .WithMany(y => y.YetkiSubeler)
                .HasForeignKey(ys => ys.YetkiId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<YetkiSube>()
                .HasOne(ys => ys.Branch)
                .WithMany()
                .HasForeignKey(ys => ys.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Sayfa>()
                .HasOne(s => s.Parent)
                .WithMany(s => s.SubPages)
                .HasForeignKey(s => s.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ⭐ İş Kazası İlişkileri
            modelBuilder.Entity<IsKazasi>()
                .HasOne(i => i.Personel)
                .WithMany()
                .HasForeignKey(i => i.PersonelId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsKazasi>()
                .HasOne(i => i.Branch)
                .WithMany()
                .HasForeignKey(i => i.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsKazasi>()
                .HasOne(i => i.Department)
                .WithMany()
                .HasForeignKey(i => i.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsKazasi>()
                .HasOne(i => i.Unit)
                .WithMany()
                .HasForeignKey(i => i.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsKazasi>()
                .HasOne(i => i.Gorev)
                .WithMany()
                .HasForeignKey(i => i.GorevId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsKazasi>()
                .HasOne(i => i.Grup)
                .WithMany()
                .HasForeignKey(i => i.GrupId)
                .OnDelete(DeleteBehavior.Restrict);

            // ⭐ İş Kazası Dosya İlişkisi
            modelBuilder.Entity<IsKazasiDosya>()
                .HasOne(d => d.IsKazasi)
                .WithMany(i => i.Dosyalar)
                .HasForeignKey(d => d.IsKazasiId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ayrıntılı İş Kazası Araştırması
            modelBuilder.Entity<IsKazasiArastirmaKategori>()
                .HasMany(x => x.Maddeler)
                .WithOne(x => x.Kategori)
                .HasForeignKey(x => x.KategoriId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsKazasiArastirma>()
                .HasOne(x => x.IsKazasi)
                .WithOne(x => x.Arastirma)
                .HasForeignKey<IsKazasiArastirma>(x => x.IsKazasiId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IsKazasiArastirma>()
                .HasOne(x => x.ArastiranPersonel)
                .WithMany()
                .HasForeignKey(x => x.ArastiranPersonelId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<IsKazasiArastirmaCevap>()
                .HasOne(x => x.Arastirma)
                .WithMany(x => x.Cevaplar)
                .HasForeignKey(x => x.IsKazasiArastirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IsKazasiArastirmaCevap>()
                .HasOne(x => x.Madde)
                .WithMany(x => x.Cevaplar)
                .HasForeignKey(x => x.MaddeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
