using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System;
using System.Linq;

[CreateAssetMenu(fileName = "Level", menuName = "ScriptableObjects/Level")]
public class LevelScriptable : SerializedScriptableObject
{
    public int BenzersizSeviyeSayisi = 50;
    public bool editordeBuSeviyeyiKullan;

    public InfList<int> BossLevels;

    [Header("Satır Sayısı Eğrisi")]
    public AnimationCurve satirSayisiEgrisi;


    #region SEVİYE KONTROLLERİ ( SeviyeAzalt , SeviyeNumarasiDegisti , SeviyeArttir )
    [HorizontalGroup("Kontroller", 0.1f)]
    [Button("<", ButtonSizes.Small)]
    public void SeviyeAzalt()
    {
        seviyeNumarasi = Mathf.Max(1, seviyeNumarasi - 1);
        SeviyeNumarasiDegisti();
    }

    [HorizontalGroup("Kontroller", 0.1f)][HideLabel][OnValueChanged("SeviyeNumarasiDegisti")] public int seviyeNumarasi = 1;

    public void SeviyeNumarasiDegisti()
    {
        if (seviyeNumarasi > BenzersizSeviyeSayisi) seviyeNumarasi = BenzersizSeviyeSayisi;
        BloklariYukle();
    }

    [HorizontalGroup("Kontroller", 0.1f)]
    [Button(">", ButtonSizes.Small)]
    public void SeviyeArttir()
    {
        seviyeNumarasi = Mathf.Min(BenzersizSeviyeSayisi, seviyeNumarasi + 1);
        SeviyeNumarasiDegisti();
    }
    #endregion


    #region BLOK ( KaydetButton , BloklariBaslat , SeviyeOlustur )
    [TableMatrix(HorizontalTitle = "BLOKLAR", SquareCells = true)]
    public Texture2D[,] BLOKLAR;

    [Button("KAYDET", ButtonSizes.Medium)]
    public void KaydetButton()
    {
        BloklariKaydet();
        EgrilereGoreSeviyeGuncelle();
    }

    [OnInspectorInit]
    private void BloklariBaslat()
    {
        if (BLOKLAR == null || BLOKLAR.GetLength(0) == 0 || BLOKLAR.GetLength(1) == 0)
        {
            BLOKLAR = new Texture2D[5, 5];
        }
    }

    public void SeviyeOlustur()
    {
        List<float> blokYogunluklari = new List<float>();
        float toplamYogunluk = 0;

        List<Block.BlockType> blokTipleri = new List<Block.BlockType>(blokYogunlukEgrileri.Keys);
        List<AnimationCurve> egriler = new List<AnimationCurve>(blokYogunlukEgrileri.Values);

        for (int i = 0; i < egriler.Count; i++)
        {
            float yogunluk = egriler[i].Evaluate((float)seviyeNumarasi / (float)BenzersizSeviyeSayisi);
            blokYogunluklari.Add(yogunluk);
            toplamYogunluk += yogunluk;
        }

        for (int i = 0; i < blokYogunluklari.Count; i++)
        {
            blokYogunluklari[i] /= toplamYogunluk;
        }

        int satirSayisi = Mathf.CeilToInt(satirSayisiEgrisi.Evaluate((float)seviyeNumarasi / BenzersizSeviyeSayisi));
        BLOKLAR = new Texture2D[satirSayisi, BLOKLAR.GetLength(1)];  // Satır sayısını güncelle

        for (int i = 0; i < satirSayisi; i++)  // BLOKLAR.GetLength(0) yerine satirSayisi kullan
        {
            for (int j = 0; j < BLOKLAR.GetLength(1); j++)
            {
                if (i == 0)
                {
                    BLOKLAR[i, j] = IndisToTexture(0); // Tahta
                    continue;
                }

                if (i == satirSayisi - 1)
                {
                    BLOKLAR[i, j] = IndisToTexture(3); // Sandık
                    continue;
                }

                if (seviyeNumarasi % 5 == 0 && i == satirSayisi - 2)
                {
                    BLOKLAR[i, j] = IndisToTexture(4); // Boss
                    continue;
                }

                float rastgeleDeger = UnityEngine.Random.value;
                float toplananYogunluk = 0;

                for (int blokTipi = 0; blokTipi < blokYogunluklari.Count; blokTipi++)
                {
                    if (blokTipi == 0 || blokTipi == 3 || blokTipi == 4) continue;

                    toplananYogunluk += blokYogunluklari[blokTipi];

                    if (rastgeleDeger <= toplananYogunluk)
                    {
                        BLOKLAR[i, j] = IndisToTexture(blokTipi);
                        break;
                    }
                }
            }
        }
    }



    Texture2D EnumToTexture(Block.BlockType blokTipi)
    {
        // Enum değerine göre ilgili Texture2D'yi döndüren kodlarınızı buraya ekleyin.
        // Örnek:
        return texturKoleksiyonu.textures[(int)blokTipi];
    }


    Texture2D IndisToTexture(int index)
    {
        return texturKoleksiyonu.textures[index];
    }




    #region bos (safe regenerate için, şuan çalışmıyo)
    public void SafeSeviyeOlustur()
    {
        int[,] currentLevelBlocks = TextureToIntTablo();
        List<float> blockDensities = new List<float>();
        float totalDensity = 0;

        //   Debug.Log($"Level {levelNum}, Total Density Before: {totalDensity}");

        List<Block.BlockType> blockTypes = new List<Block.BlockType>(blokYogunlukEgrileri.Keys);
        List<AnimationCurve> curves = new List<AnimationCurve>(blokYogunlukEgrileri.Values);

        for (int i = 0; i < curves.Count; i++)
        {
            float density = curves[i].Evaluate((float)seviyeNumarasi / (float)BenzersizSeviyeSayisi);
            blockDensities.Add(density);
            totalDensity += density;

            //    Debug.Log($"Density: {density}, Total Density After: {totalDensity}");
        }

        Dictionary<int, int> currentDistribution = GetCurrentBlockDistribution();


        Dictionary<int, int> idealDistribution = CalculateIdealBlockDistribution();


        Dictionary<int, int> differences = new Dictionary<int, int>();

        foreach (var pair in idealDistribution) differences[pair.Key] = pair.Value - currentDistribution[pair.Key];


        for (int i = 0; i < currentLevelBlocks.GetLength(0); i++)
        {
            for (int j = 0; j < currentLevelBlocks.GetLength(1); j++)
            {
                int currentBlockType = currentLevelBlocks[i, j];

                if (differences.ContainsKey(currentBlockType)) differences[currentBlockType]--;
                else Debug.LogError($"Key {currentBlockType} not found in differences dictionary.");

            }
        }

        Debug.Log("Current Distribution: " + DictionaryToString(currentDistribution));
        Debug.Log("Ideal Distribution: " + DictionaryToString(idealDistribution));
        Debug.Log("Differences: " + DictionaryToString(differences));

        // Debug.Log("Differences After: " + DictionaryToString(differences));


        // Debugging: Differences After
        //  Debug.Log("Differences After: " + string.Join(", ", differences.Select(kv => kv.Key + "=" + kv.Value).ToArray()));

        AdjustBlocksBasedOnDifferences(differences);

    }
    private Dictionary<int, int> CalculateIdealBlockDistribution()
    {
        Dictionary<int, int> distribution = new Dictionary<int, int>();

        List<float> blockDensities = new List<float>();
        float totalDensity = 0;

        // BlockType ve AnimationCurve listelerini elde edelim
        List<Block.BlockType> blockTypes = new List<Block.BlockType>(blokYogunlukEgrileri.Keys);
        List<AnimationCurve> curves = new List<AnimationCurve>(blokYogunlukEgrileri.Values);

        // Curve'lerden yoğunluk değerlerini al ve toplamını bul
        for (int i = 0; i < curves.Count; i++)
        {
            float density = curves[i].Evaluate((float)seviyeNumarasi / BenzersizSeviyeSayisi);
            blockDensities.Add(density);
            totalDensity += density;
        }

        int totalBlocks = BLOKLAR.GetLength(0) * BLOKLAR.GetLength(1);

        for (int i = 0; i < blockDensities.Count; i++)
        {
            int idealCount = Mathf.RoundToInt(blockDensities[i] / totalDensity * totalBlocks);
            distribution[i] = idealCount;
        }

        return distribution;
    }
    private Dictionary<int, int> GetCurrentBlockDistribution()
    {
        Dictionary<int, int> distribution = new Dictionary<int, int>();

        for (int i = 0; i < BLOKLAR.GetLength(0); i++)
        {
            for (int j = 0; j < BLOKLAR.GetLength(1); j++)
            {
                int blockType = TextureToInt(BLOKLAR[i, j]);

                if (distribution.ContainsKey(blockType))
                {
                    distribution[blockType]++;
                }
                else
                {
                    distribution[blockType] = 1;
                }
            }
        }

        return distribution;
    }
    private string DictionaryToString(Dictionary<int, int> dictionary)
    {
        List<string> items = new List<string>();
        foreach (var pair in dictionary)
        {
            string blockName = Enum.GetName(typeof(Block.BlockType), pair.Key); // Anahtar int değerini blok tipi ismine çevir
            items.Add($"{blockName}: {pair.Value}");
        }
        return "{ " + string.Join(", ", items) + " }";
    }
    private void AdjustBlocksBasedOnDifferences(Dictionary<int, int> differences)
    {
        for (int i = 0; i < BLOKLAR.GetLength(0); i++)
        {
            for (int j = 0; j < BLOKLAR.GetLength(1); j++)
            {
                int currentBlockType = TextureToInt(BLOKLAR[i, j]);
                int idealBlockType = GetIdealBlockType(differences);  // Yeni bir fonksiyon ile ideal blok tipini döndürebiliriz
                Debug.Log("IDEALBLOCKTYPE index: " + idealBlockType);

                if (idealBlockType == -1)
                {
                    // Eğer ideal blok tipi bulunamıyorsa bu bloğu atla
                    continue;
                }

                if (currentBlockType != idealBlockType /*&& CanBeChanged(i, j)*/)  // CanBeChanged, bu bloğun değiştirilip değiştirilemeyeceğini kontrol eden başka bir fonksiyon olabilir
                {
                    BLOKLAR[i, j] = IntToTexture(idealBlockType);
                    differences[currentBlockType]--;
                    differences[idealBlockType]++;
                }
            }
        }
    }
    private int GetIdealBlockType(Dictionary<int, int> differences)
    {
        if (differences == null || differences.Count == 0)
        {
            Debug.LogError("Differences dictionary is null or empty.");
            return -1; // veya istediğiniz bir hata kodu veya değeri döndürün
        }

        int idealBlockType = 0;
        int maxDifference = int.MinValue;

        foreach (var pair in differences)
        {
            if (pair.Value > maxDifference)
            {
                maxDifference = pair.Value;
                idealBlockType = pair.Key;
            }
        }

        if (maxDifference <= 0)
        {
            Debug.LogWarning("No positive difference found. Current block distribution may already be optimal, or there are more blocks than needed.");
            return -1; // veya istediğiniz bir hata kodu veya değeri döndürün
        }

        return idealBlockType;
    }
    #endregion

    Texture2D IntToTexture(int index) => texturKoleksiyonu.textures[index];
    int TextureToInt(Texture2D texture) => texturKoleksiyonu.textures.IndexOf(texture);

    #endregion

    #region REGENERATE ( MevcutSeviyeyiRegenerate ,TumSeviyeleriRegenerate )
    [Button("Mevcut Seviyeyi Yeniden Oluştur", ButtonSizes.Medium)]
    public void MevcutSeviyeyiRegenerate()
    {
        SeviyeOlustur();
    }

    [Button("Tüm Seviyeleri Yeniden Oluştur", ButtonSizes.Medium)]
    public void TumSeviyeleriRegenerate()
    {
        for (int i = 1; i <= BenzersizSeviyeSayisi; i++)
        {
            seviyeNumarasi = i;
            SeviyeOlustur();
            BloklariKaydet();
        }
    }

    /* 
    [Button("SAFE Regenerate All Levels", ButtonSizes.Medium)]
     public void SafeRegenerateAllLevels()
     { for (int i = 1; i <= BenzersizSeviyeSayisi; i++) { seviyeNumarasi = i; BloklariYukle(); SafeSeviyeOlustur(); BloklariKaydet(); } }
    */
    #endregion

    #region DOSYA ( TextureToIntTablo , BloklariKaydet , BloklariYukle )
    string temelKayitYolu = "Assets/Database/Levels";  // Kayıt yapılacak temel klasör yolu

    public int[,] TextureToIntTablo()
    {
        int[,] bloklarInt = new int[BLOKLAR.GetLength(0), BLOKLAR.GetLength(1)];

        for (int i = 0; i < BLOKLAR.GetLength(0); i++)
        {
            for (int j = 0; j < BLOKLAR.GetLength(1); j++)
            {
                bloklarInt[i, j] = TextureToInt(BLOKLAR[i, j]);
            }
        }

        return bloklarInt;
    }
    /*
        public int[,] KayitliIntTablosunuYukle(int seviyeNumarasi)
        {
                string yol = $"{temelKayitYolu}/Seviye_{seviyeNumarasi}.dat";

                if (!File.Exists(yol))
                {
                    Debug.LogError($"{yol} dosyası bulunamadı.");
                    return null;
                }

                BinaryFormatter formatter = new BinaryFormatter();
                FileStream stream = new FileStream(yol, FileMode.Open);
                int[,] bloklarInt = formatter.Deserialize(stream) as int[,];
                stream.Close();

                if (bloklarInt == null)
                {
                    Debug.LogError($"Seviye {seviyeNumarasi} verisi okunamadı veya deserializasyon hatası.");
                    return null;
                }

                return bloklarInt;
        }
    */

    public void BloklariKaydet()
    {
        int[,] bloklarInt = TextureToIntTablo();

        BinaryFormatter formatter = new BinaryFormatter();
        string yol = $"{temelKayitYolu}/Seviye_{seviyeNumarasi}.dat";

        string dizinYolu = Path.GetDirectoryName(yol);
        if (!Directory.Exists(dizinYolu))
        {
            Directory.CreateDirectory(dizinYolu);
        }

        try
        {
            FileStream stream = new FileStream(yol, FileMode.Create);
            formatter.Serialize(stream, bloklarInt);
            stream.Close();
        }
        catch (Exception hata)
        {
            Debug.LogError("Kaydederken hata: " + hata.Message);
        }
    }

    public void BloklariYukle()
    {
        string yol = $"{temelKayitYolu}/Seviye_{seviyeNumarasi}.dat";

        if (File.Exists(yol))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(yol, FileMode.Open);

            int[,] bloklarInt = formatter.Deserialize(stream) as int[,];
            stream.Close();

            // BLOKLAR dizisini bloklarInt boyutuna göre yeniden tanımlıyoruz
            BLOKLAR = new Texture2D[bloklarInt.GetLength(0), bloklarInt.GetLength(1)];

            for (int i = 0; i < bloklarInt.GetLength(0); i++)
            {
                for (int j = 0; j < bloklarInt.GetLength(1); j++)
                {
                    BLOKLAR[i, j] = IndisToTexture(bloklarInt[i, j]);
                }
            }
        }
        else
        {
            Debug.LogError("Dosya bulunamadı: " + yol);
        }
    }
    #endregion


    #region CURVE GÜNCELLEMELERİ ( EgrilereGoreSeviyeGuncelle , MevcutSeviyedeBlokOranlariniHesapla )
    public TextureCollection texturKoleksiyonu;

    [InlineProperty]
    public Dictionary<Block.BlockType, AnimationCurve> blokYogunlukEgrileri;

    public void EgrilereGoreSeviyeGuncelle()
    {
        Dictionary<int, float> blokOranlari = MevcutSeviyedeBlokOranlariniHesapla();

        foreach (var pair in blokOranlari)
        {
            int blokTipi = pair.Key;
            float oran = pair.Value;

            // İlgili eğriyi al ve bu seviye için değerini güncelle
            if (blokYogunlukEgrileri.ContainsKey((Block.BlockType)blokTipi))
            {
                blokYogunlukEgrileri[(Block.BlockType)blokTipi].AddKey((float)seviyeNumarasi / (float)BenzersizSeviyeSayisi, oran);
            }
        }
    }

    private Dictionary<int, float> MevcutSeviyedeBlokOranlariniHesapla()
    {
        Dictionary<int, int> blokSayilari = new Dictionary<int, int>();
        int toplamBloklar = 0;

        for (int i = 0; i < BLOKLAR.GetLength(0); i++)
        {
            for (int j = 0; j < BLOKLAR.GetLength(1); j++)
            {
                int blokTipi = TextureToInt(BLOKLAR[i, j]);

                if (!blokSayilari.ContainsKey(blokTipi))
                {
                    blokSayilari[blokTipi] = 0;
                }

                blokSayilari[blokTipi]++;
                toplamBloklar++;
            }
        }

        Dictionary<int, float> blokOranlari = new Dictionary<int, float>();

        foreach (var pair in blokSayilari)
        {
            blokOranlari[pair.Key] = (float)pair.Value / toplamBloklar;
        }

        return blokOranlari;
    }
    #endregion
}
