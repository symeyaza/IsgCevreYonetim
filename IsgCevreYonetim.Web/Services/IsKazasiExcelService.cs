using IsgCevreYonetim.Domain.Entities;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System.Globalization;
using System.Text;

namespace IsgCevreYonetim.Web.Services
{
    /// <summary>
    /// 150-FR-513 Ayrıntılı Kaza Araştırma Raporu şablonunu bozmadan
    /// kaza bilgilerini ve seçili araştırma maddelerini doldurur.
    /// </summary>
    public class IsKazasiExcelService
    {
        private readonly IWebHostEnvironment _environment;

        public IsKazasiExcelService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public byte[] Olustur(IsKazasi kaza, IReadOnlyCollection<IsKazasiArastirmaMadde> seciliMaddeler, IsKazasiArastirma? arastirma, IReadOnlyCollection<IsKazasiArastirmaMadde>? tumMaddeler = null, IReadOnlyCollection<IsKazasiDuzelticiFaaliyet>? duzelticiFaaliyetler = null)
        {
            // Şablon publish sırasında fiziksel olarak Templates klasörüne kopyalanır.
            // Buna ek olarak DLL içine EmbeddedResource olarak da gömülüdür. Böylece bazı
            // hosting panelleri Templates klasörünü atlamış olsa bile rapor üretilebilir.
            using var input = SablonAkisiniAc();
            var workbook = new HSSFWorkbook(input);

            // Formun üst bölümü birleşik hücrelerden oluştuğu için değerleri
            // etiket arayarak değil, şablondaki gerçek veri hücrelerine yazıyoruz.
            // Böylece değerler etiketin birleşik hücresinin içine gizlenmez.
            for (int s = 0; s < workbook.NumberOfSheets; s++)
            {
                var sheet = workbook.GetSheetAt(s);
                DoldurUstBilgiler(sheet, kaza, arastirma);
                DoldurDuzelticiFaaliyetler(sheet, duzelticiFaaliyetler);
                IsaretleSeciliMaddeler(sheet, seciliMaddeler, tumMaddeler);
            }

            using var output = new MemoryStream();
            workbook.Write(output, leaveOpen: true);
            return output.ToArray();
        }


        private Stream SablonAkisiniAc()
        {
            const string fileName = "KazaArastirmaRaporu.xls";

            // 1) Normal çalışma/publish konumu.
            var contentRootPath = System.IO.Path.Combine(_environment.ContentRootPath, "Templates", fileName);
            if (System.IO.File.Exists(contentRootPath))
                return new FileStream(contentRootPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // 2) IIS/hosting çalışma dizini farklı olduğunda uygulama DLL'inin yanını dene.
            var baseDirectoryPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Templates", fileName);
            if (System.IO.File.Exists(baseDirectoryPath))
                return new FileStream(baseDirectoryPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // 3) Son güvence: şablon IsgCevreYonetim.Web.dll içine gömülü kaynak olarak eklenmiştir.
            var assembly = typeof(IsKazasiExcelService).Assembly;
            var resourceStream = assembly.GetManifestResourceStream("IsgCevreYonetim.Web.Templates.KazaArastirmaRaporu.xls");
            if (resourceStream != null)
                return resourceStream;

            throw new FileNotFoundException(
                $"Kaza araştırma Excel şablonu bulunamadı. Kontrol edilen yollar: '{contentRootPath}', '{baseDirectoryPath}' ve gömülü kaynak.");
        }

        private static void DoldurUstBilgiler(ISheet sheet, IsKazasi kaza, IsKazasiArastirma? arastirma)
        {
            // Hücre adresleri, kullanıcının verdiği 150-FR-513 Rev.3 XLS formundaki
            // gerçek birleşik alanların SOL-ÜST hücreleridir. NPOI birleşik hücreye
            // yalnızca bu hücre üzerinden değer yazmalıdır.
            Yaz(sheet, 5, 5, kaza.Branch?.BranchAdi ?? kaza.Personel?.Branch?.BranchAdi ?? "");             // F6:Q6 Fabrika/Lokasyon
            Yaz(sheet, 6, 5, kaza.Department?.DepartmentAdi ?? kaza.Personel?.Department?.DepartmentAdi ?? ""); // F7:Q7 İşletme/Birim = Departman
            Yaz(sheet, 5, 20, Tarih(arastirma?.ArastirmaTarihi));                                            // U6:Y6 Rapor Tarihi
            Yaz(sheet, 6, 20, Tarih(kaza.KazaTarihi));                                                       // U7:Y7 Kaza Tarihi
            Yaz(sheet, 7, 4, kaza.Id.ToString(CultureInfo.InvariantCulture));                                // E8 ve devamı Kaza Rapor No

            Yaz(sheet, 12, 4, kaza.PersonelAdSoyad ?? kaza.Personel?.FullName ?? "");                    // E13:O13 Adı Soyadı
            Yaz(sheet, 12, 16, Yas(kaza.Personel?.DogumTarihi, kaza.KazaTarihi));                           // Q13:T13 Yaşı (kaza tarihindeki yaş)
            // Cinsiyet alanı sabit hücre adresleriyle doldurulur:
            // U13 = etiket, V13 = kazazedenin cinsiyeti.
            YazZorunlu(sheet, 12, 20, "Cinsiyet:");                                                       // U13
            Yaz(sheet, 12, 21, kaza.Personel?.Cinsiyet?.Ad ?? "");                                       // V13
            var baslamaSaati = VardiyaBaslangicSaati(kaza.Vardiya?.VardiyaAdi);
            YazZorunlu(sheet, 13, 1, $"Çalışmaya Başladığı Saat: {baslamaSaati}");                         // B14
            Yaz(sheet, 13, 16, kaza.Gorev?.Ad ?? kaza.Personel?.Gorev?.Ad ?? "");                        // Q14... İşi
            Yaz(sheet, 13, 21, kaza.Gorev?.Ad ?? kaza.Personel?.Gorev?.Ad ?? "");                        // V14... Ünvanı
            YazZorunlu(sheet, 14, 1, $"Sorumlu amiri: {kaza.SorumluAmirAdSoyad ?? kaza.SorumluAmirPersonel?.FullName ?? string.Empty}");
            Yaz(sheet, 14, 18, Tarih(kaza.IseGirisTarihi ?? kaza.Personel?.IseGirisTarihi));                 // Q15... İşe Giriş Tarihi
            YazZorunlu(sheet, 15, 17, $"Süre: {CalismaSuresi(kaza.KazaTarihi, kaza.Vardiya?.VardiyaAdi)}"); // R16

            // Formun kendi başlığını koruyup dinamik değeri aynı birleşik hücreye yaz.
            YazZorunlu(sheet, 16, 1, $"Kazanın Tarifi/Oluş Şekli: {kaza.Aciklama ?? string.Empty}");       // B17:Y19
            var sahitler = kaza.Sahitler == null
                ? string.Empty
                : string.Join(", ", kaza.Sahitler
                    .Where(x => x.IsActive && !x.IsDeleted)
                    .OrderBy(x => x.AdSoyad)
                    .Select(x => x.AdSoyad));
            YazZorunlu(sheet, 19, 1, $"Kaza Şahitleri: {sahitler}");                                      // B20:Y21
            YazZorunlu(sheet, 21, 1, $"Gün Kaybı: {(kaza.KayipGun ?? 0).ToString(CultureInfo.InvariantCulture)}"); // B22:Y22

            // Araştırmayı yapan kişi kayıtlıysa formun ARAŞTIRMA EKİBİ bölümüne de aktar.
            if (arastirma?.ArastiranPersonel != null)
            {
                Yaz(sheet, 58, 1, arastirma.ArastiranPersonel.FullName);                                      // B59
                Yaz(sheet, 58, 7, arastirma.ArastiranPersonel.Gorev?.Ad ?? "");                           // H59
            }
        }


        private static void DoldurDuzelticiFaaliyetler(ISheet sheet, IReadOnlyCollection<IsKazasiDuzelticiFaaliyet>? faaliyetler)
        {
            var liste = faaliyetler?.Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.CreatedDate).ToList()
                ?? new List<IsKazasiDuzelticiFaaliyet>();
            // Şablonda B54, B55 ve B56 düzeltici faaliyet satırlarıdır.
            for (var i = 0; i < 3; i++)
            {
                var metin = i < liste.Count
                    ? $"{i + 1}- {liste[i].Baslik}: {liste[i].Aciklama}"
                    : $"{i + 1}-";
                YazZorunlu(sheet, 53 + i, 1, metin);
            }
            // Üçten fazla faaliyet varsa üçüncü satırda kalanları da kaybetmeden özetle.
            if (liste.Count > 3)
            {
                var kalan = string.Join(" | ", liste.Skip(2).Select((x, i) => $"{i + 3}- {x.Baslik}: {x.Aciklama}"));
                YazZorunlu(sheet, 55, 1, kalan);
            }
        }

        private static string VardiyaBaslangicSaati(string? vardiya)
        {
            if (string.IsNullOrWhiteSpace(vardiya)) return string.Empty;
            var ilk = vardiya.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(ilk)) return string.Empty;
            return ilk == "24:00" ? "00:00" : ilk;
        }

        private static string CalismaSuresi(DateTime kazaTarihi, string? vardiya)
        {
            var saat = VardiyaBaslangicSaati(vardiya);
            if (!TimeSpan.TryParseExact(saat, @"hh\:mm", CultureInfo.InvariantCulture, out var baslangic))
                return string.Empty;

            var kazaSaati = kazaTarihi.TimeOfDay;
            var sure = kazaSaati - baslangic;
            if (sure < TimeSpan.Zero) sure += TimeSpan.FromDays(1);

            var toplamDakika = (int)Math.Floor(sure.TotalMinutes);
            return $"{toplamDakika / 60} saat {toplamDakika % 60} dakika";
        }

        private static void YazZorunlu(ISheet sheet, int rowIndex, int columnIndex, string value)
        {
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            var cell = row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
            cell.SetCellValue(value ?? string.Empty);
        }

        private static void Yaz(ISheet sheet, int rowIndex, int columnIndex, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            var cell = row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
            cell.SetCellValue(value);
        }

        private static void Ekle(ISheet sheet, int rowIndex, int columnIndex, string baslik, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            var cell = row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
            cell.SetCellValue($"{baslik} {value}");
        }

        private static void DoldurGenelBilgiler(ISheet sheet, Dictionary<string, string> alanlar)
        {
            for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row == null) continue;

                for (int c = row.FirstCellNum < 0 ? 0 : row.FirstCellNum; c < row.LastCellNum; c++)
                {
                    var cell = row.GetCell(c);
                    var text = HucreMetni(cell);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    foreach (var alan in alanlar)
                    {
                        if (!Eslesiyor(text, alan.Key)) continue;
                        if (string.IsNullOrWhiteSpace(alan.Value)) break;

                        var hedef = DegerHucresiBul(sheet, r, c);
                        if (hedef != null)
                            DegerYaz(hedef, alan.Value);
                        break;
                    }
                }
            }
        }

        private static void IsaretleSeciliMaddeler(ISheet sheet, IReadOnlyCollection<IsKazasiArastirmaMadde> seciliMaddeler, IReadOnlyCollection<IsKazasiArastirmaMadde>? tumMaddeler)
        {
            // Şablon daha önce doldurulmuş bir rapordan alınmış olsa bile eski tikler
            // yeni kazaya taşınmamalı. Önce formdaki TÜM seçim kutularını temizliyoruz.
            SecimKutulariniTemizle(sheet);

            if (seciliMaddeler.Count == 0) return;

            var secili = seciliMaddeler
                .Where(x => !string.IsNullOrWhiteSpace(x.Metin))
                .Select(x => new
                {
                    Madde = x,
                    Metin = Normalize(x.Metin),
                    Kategori = Normalize(x.Kategori?.Ad)
                })
                .ToList();

            var alanlar = new (int IlkSatir, int SonSatir, int MetinSutun, int TikSutun, string KategoriAnahtari)[]
            {
                (23, 34, 1,  9,  "yaralanma-turu"),    // B24:B35 -> J24:J35  Yaralanma Türü
                (23, 34, 10, 18, "yaralanan-bolge"),  // K24:K35 -> S24:S35  Yaralanan Bölge
                (23, 34, 19, 24, "yaralayici-etken"), // T24:T35 -> Y24:Y35  Yaralayıcı Etken
                (36, 41, 1,  12, "birincil-neden"),   // B37:B42 -> M37:M42  Birincil Neden sol
                (36, 41, 13, 24, "birincil-neden"),   // N37:N42 -> Y37:Y42  Birincil Neden sağ
                (43, 50, 1,  10, "kok-neden"),        // B44:B51 -> K44:K51  Kök Neden sol
                (43, 50, 11, 24, "kok-neden"),        // L44:L51 -> Y44:Y51  Kök Neden sağ
            };

            // Bir madde en fazla BİR kutuyu işaretleyebilir. Önce form metniyle kesin/benzer
            // eşleşme yapılır; aynı metin (özellikle "Diğer") farklı kategorilerde bulunsa
            // bile yalnız kendi Excel alanında aranır. Bulunan madde fallback aşamalarında
            // tekrar kullanılmaz.
            var isaretlenen = new HashSet<int>();
            foreach (var alan in alanlar)
            {
                for (int r = alan.IlkSatir; r <= alan.SonSatir; r++)
                {
                    var row = sheet.GetRow(r);
                    if (row == null) continue;
                    var formMetni = Normalize(HucreMetni(row.GetCell(alan.MetinSutun)));
                    if (string.IsNullOrWhiteSpace(formMetni)) continue;

                    var eslesen = secili.FirstOrDefault(x =>
                        !isaretlenen.Contains(x.Madde.Id) &&
                        KategoriAlaniUyusuyor(x.Kategori, alan.KategoriAnahtari) &&
                        BenzerMetin(formMetni, x.Metin));
                    if (eslesen == null) continue;

                    TikYaz(row.GetCell(alan.TikSutun) ?? row.CreateCell(alan.TikSutun));
                    isaretlenen.Add(eslesen.Madde.Id);
                }
            }

            // Yalnızca henüz işaretlenmemiş maddeler sıra bilgisiyle denenir.
            foreach (var madde in seciliMaddeler.Where(x => !isaretlenen.Contains(x.Id)))
            {
                if (KategoriSirasiIleIsaretle(sheet, madde))
                    isaretlenen.Add(madde.Id);
            }

            // Son güvence de SADECE hâlâ işaretlenmemiş maddeler içindir.
            // Önceki kod burada tüm seçili maddeleri tekrar işaretlediği için tek seçim
            // hem metin eşleşmesinden hem kategori sırasından iki ayrı kutuya düşebiliyordu.
            if (tumMaddeler != null && tumMaddeler.Count > 0)
            {
                var kalanlar = seciliMaddeler.Where(x => !isaretlenen.Contains(x.Id)).ToList();
                NedenleriKategoriListeSirasiIleIsaretle(sheet, kalanlar, tumMaddeler);
            }
        }

        private static bool KategoriAlaniUyusuyor(string kategori, string alanAnahtari)
        {
            if (string.IsNullOrWhiteSpace(kategori)) return false;

            return alanAnahtari switch
            {
                "yaralanma-turu" =>
                    kategori.Contains("yaralanma") &&
                    (kategori.Contains("türü") || kategori.Contains("turu")),

                "yaralanan-bolge" =>
                    kategori.Contains("yaralanan") &&
                    (kategori.Contains("bölge") || kategori.Contains("bolge")),

                "yaralayici-etken" =>
                    (kategori.Contains("yaralayıcı") || kategori.Contains("yaralayici")) &&
                    kategori.Contains("etken"),

                "birincil-neden" =>
                    (kategori.Contains("birincil") || kategori.Contains("birinicil")) &&
                    kategori.Contains("neden"),

                "kok-neden" =>
                    (kategori.Contains("kök") || kategori.Contains("kok")) &&
                    kategori.Contains("neden"),

                _ => false
            };
        }

        private static void SecimKutulariniTemizle(ISheet sheet)
        {
            // J24:J35, S24:S35, Y24:Y35
            for (int r = 23; r <= 34; r++)
            {
                Temizle(sheet, r, 9);
                Temizle(sheet, r, 18);
                Temizle(sheet, r, 24);
            }

            // Birincil Nedenler: M37:M42 ve Y37:Y42
            for (int r = 36; r <= 41; r++)
            {
                Temizle(sheet, r, 12);
                Temizle(sheet, r, 24);
            }

            // Kök Nedenler: K44:K51 ve Y44:Y51
            for (int r = 43; r <= 50; r++)
            {
                Temizle(sheet, r, 10);
                Temizle(sheet, r, 24);
            }
        }

        private static void Temizle(ISheet sheet, int rowIndex, int columnIndex)
        {
            var row = sheet.GetRow(rowIndex);
            var cell = row?.GetCell(columnIndex);
            if (cell != null)
                cell.SetCellValue(string.Empty); // stil/kenarlık korunur, yalnız eski tik silinir
        }

        private static void NedenleriKategoriListeSirasiIleIsaretle(
            ISheet sheet,
            IReadOnlyCollection<IsKazasiArastirmaMadde> seciliMaddeler,
            IReadOnlyCollection<IsKazasiArastirmaMadde> tumMaddeler)
        {
            if (seciliMaddeler.Count == 0) return;
            var seciliIdler = seciliMaddeler.Select(x => x.Id).ToHashSet();

            foreach (var kategoriGrubu in tumMaddeler.GroupBy(x => x.KategoriId))
            {
                var ornek = kategoriGrubu.FirstOrDefault();
                var kategoriAdi = Normalize(ornek?.Kategori?.Ad);
                var birincil = kategoriAdi.Contains("birincil") || kategoriAdi.Contains("birinicil");
                var kok = kategoriAdi.Contains("kok neden") || kategoriAdi.Contains("kokneden");
                if (!birincil && !kok) continue;

                var maddeler = kategoriGrubu
                    .OrderBy(x => x.Sira)
                    .ThenBy(x => x.Metin)
                    .ThenBy(x => x.Id)
                    .ToList();

                for (var i = 0; i < maddeler.Count; i++)
                {
                    if (!seciliIdler.Contains(maddeler[i].Id)) continue;
                    var sira = i + 1;
                    int rowIndex;
                    int columnIndex;

                    if (birincil)
                    {
                        if (sira > 12) continue;
                        if (sira <= 6) { rowIndex = 35 + sira; columnIndex = 12; }
                        else { rowIndex = 35 + (sira - 6); columnIndex = 24; }
                    }
                    else
                    {
                        if (sira > 16) continue;
                        if (sira <= 8) { rowIndex = 42 + sira; columnIndex = 10; }
                        else { rowIndex = 42 + (sira - 8); columnIndex = 24; }
                    }

                    var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
                    TikYaz(row.GetCell(columnIndex) ?? row.CreateCell(columnIndex));
                }
            }
        }

        private static bool KategoriSirasiIleIsaretle(ISheet sheet, IsKazasiArastirmaMadde madde)
        {
            var kategori = Normalize(madde.Kategori?.Ad);
            if (madde.Sira <= 0) return false;

            int rowIndex, columnIndex;
            if (kategori.Contains("birincil") || kategori.Contains("birinicil") || kategori.Contains("birincil neden"))
            {
                if (madde.Sira <= 6) { rowIndex = 35 + madde.Sira; columnIndex = 12; }
                else if (madde.Sira <= 12) { rowIndex = 35 + madde.Sira - 6; columnIndex = 24; }
                else return false;
            }
            else if (kategori.Contains("kok neden") || kategori.Contains("kök neden") || kategori.Contains("kokneden"))
            {
                if (madde.Sira <= 8) { rowIndex = 42 + madde.Sira; columnIndex = 10; }
                else if (madde.Sira <= 16) { rowIndex = 42 + madde.Sira - 8; columnIndex = 24; }
                else return false;
            }
            else return false;

            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            TikYaz(row.GetCell(columnIndex) ?? row.CreateCell(columnIndex));
            return true;
        }

        private static ICell? DegerHucresiBul(ISheet sheet, int rowIndex, int labelColumn)
        {
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            // Önce başlığın sağındaki ilk uygun/boş hücreyi kullan.
            for (int c = labelColumn + 1; c <= Math.Min(labelColumn + 6, 255); c++)
            {
                var candidate = row.GetCell(c) ?? row.CreateCell(c);
                var value = HucreMetni(candidate);
                if (string.IsNullOrWhiteSpace(value)) return candidate;
            }
            return row.GetCell(labelColumn + 1) ?? row.CreateCell(labelColumn + 1);
        }

        private static ICell? IsaretHucresiBul(ISheet sheet, int rowIndex, int textColumn)
        {
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);

            // 150-FR-513 formundaki kutucuk sütunları SABİTTİR.
            // Burada en yakın boş hücre aranmaz; aksi halde tikler metnin başına/arasına kayar.
            // NPOI yalnızca mevcut kutucuk hücresine değer yazar; sütun genişliği, satır yüksekliği,
            // kenarlıklar ve birleşik hücreler şablondaki haliyle korunur.
            int? checkColumn = null;

            // Excel satır 24-35: Yaralanma Türü / Yaralanan Bölge / Yaralayıcı Etken
            // B:I metin -> J kutu, K:R metin -> S kutu, T:X metin -> Y kutu
            if (rowIndex >= 23 && rowIndex <= 34)
            {
                if (textColumn >= 1 && textColumn <= 8) checkColumn = 9;       // J
                else if (textColumn >= 10 && textColumn <= 17) checkColumn = 18; // S
                else if (textColumn >= 19 && textColumn <= 23) checkColumn = 24; // Y
            }
            // Excel satır 37-42: Kazanın Birincil Nedenleri
            // B:L metin -> M kutu, N:X metin -> Y kutu
            else if (rowIndex >= 36 && rowIndex <= 41)
            {
                if (textColumn >= 1 && textColumn <= 11) checkColumn = 12;     // M
                else if (textColumn >= 13 && textColumn <= 23) checkColumn = 24; // Y
            }
            // Excel satır 44-51: Kazanın Kök Nedenleri
            // B:J metin -> K kutu, L:X metin -> Y kutu
            else if (rowIndex >= 43 && rowIndex <= 50)
            {
                if (textColumn >= 1 && textColumn <= 9) checkColumn = 10;      // K
                else if (textColumn >= 11 && textColumn <= 23) checkColumn = 24; // Y
            }

            if (!checkColumn.HasValue) return null;
            return row.GetCell(checkColumn.Value) ?? row.CreateCell(checkColumn.Value);
        }

        private static void TikYaz(ICell cell)
        {
            cell.SetCellValue("✓");
            var style = cell.CellStyle;
            if (style != null)
            {
                cell.CellStyle = style;
            }
        }

        private static void DegerYaz(ICell cell, string value)
        {
            cell.SetCellValue(value);
        }

        private static string HucreMetni(ICell? cell)
        {
            if (cell == null) return "";
            return cell.CellType switch
            {
                CellType.String => cell.StringCellValue?.Trim() ?? "",
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                    ? (cell.DateCellValue?.ToString("dd.MM.yyyy") ?? "")
                    : cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
                CellType.Boolean => cell.BooleanCellValue ? "Evet" : "Hayır",
                CellType.Formula => cell.ToString()?.Trim() ?? "",
                _ => cell.ToString()?.Trim() ?? ""
            };
        }

        private static bool Eslesiyor(string cellText, string label)
        {
            var a = Normalize(cellText);
            var b = Normalize(label);
            return a == b || a.StartsWith(b) || (b.Length >= 6 && a.Contains(b));
        }

        private static bool BenzerMetin(string a, string b)
        {
            if (a == b) return true;
            if (a.Length >= 12 && b.Contains(a)) return true;
            if (b.Length >= 12 && a.Contains(b)) return true;
            return false;
        }

        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var text = value.Trim().ToLower(new CultureInfo("tr-TR"));
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (char.IsWhiteSpace(ch)) sb.Append(' ');
            }
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static string Tarih(DateTime? value) => value?.ToString("dd.MM.yyyy") ?? "";

        private static string Yas(DateTime? dogumTarihi, DateTime referansTarihi)
        {
            if (!dogumTarihi.HasValue || dogumTarihi.Value.Date > referansTarihi.Date)
                return "";

            var dogum = dogumTarihi.Value.Date;
            var referans = referansTarihi.Date;
            var yas = referans.Year - dogum.Year;
            if (dogum > referans.AddYears(-yas)) yas--;
            return yas.ToString(CultureInfo.InvariantCulture);
        }
    }
}
