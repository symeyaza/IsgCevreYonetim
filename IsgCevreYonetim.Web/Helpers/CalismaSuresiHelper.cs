using System;
using System.Collections.Generic;

namespace IsgCevreYonetim.Web.Helpers;

public static class CalismaSuresiHelper
{
    public static string Hesapla(DateTime? iseGirisTarihi, DateTime? bugun = null)
    {
        if (!iseGirisTarihi.HasValue) return "Belirtilmemiş";
        var baslangic = iseGirisTarihi.Value.Date;
        var bitis = (bugun ?? DateTime.Today).Date;
        if (baslangic > bitis) return "İşe giriş tarihi henüz gelmedi";

        int yil = bitis.Year - baslangic.Year;
        if (baslangic.AddYears(yil) > bitis) yil--;
        var yilSonrasi = baslangic.AddYears(yil);
        int ay = 0;
        while (ay < 11 && yilSonrasi.AddMonths(ay + 1) <= bitis) ay++;
        int gun = (bitis - yilSonrasi.AddMonths(ay)).Days;
        var kisimlar = new List<string>();
        if (yil > 0) kisimlar.Add($"{yil} yıl");
        if (ay > 0) kisimlar.Add($"{ay} ay");
        if (gun > 0 || kisimlar.Count == 0) kisimlar.Add($"{gun} gün");
        return string.Join(" ", kisimlar);
    }
}
