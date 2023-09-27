using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System;

[CreateAssetMenu(fileName = "Level", menuName = "ScriptableObjects/Level")]
public class LevelScriptable : SerializedScriptableObject
{
    [HorizontalGroup("Controls", 0.1f)]
    [Button("<", ButtonSizes.Small)]
    public void DecreaseLevel()
    {
        levelNumber = Mathf.Max(1, levelNumber - 1);
        OnLevelNumberChanged();
    }

    [HorizontalGroup("Controls", 0.1f)]
    [HideLabel]
    [OnValueChanged("OnLevelNumberChanged")]
    public int levelNumber = 1;

    private void OnLevelNumberChanged() { LoadLevelBlocks(); }

    [HorizontalGroup("Controls", 0.1f)]
    [Button(">", ButtonSizes.Small)]
    public void IncreaseLevel()
    {
        levelNumber++;
        OnLevelNumberChanged();
    }

    [TableMatrix(HorizontalTitle = "BLOCKS", SquareCells = true)]
    // [TextureFolder("Assets/Database/Textures")]  // Bu yolu kendi texture'larınızın yoluna göre güncelleyin
    public Texture2D[,] BLOCKS;
    [Button("KAYDET", ButtonSizes.Medium)]
    public void ManuallySaveLevelBlocks() { SaveLevelBlocks(); UpdateCurvesBasedOnLevel(); }

    [OnInspectorInit]
    private void InitBlocks()
    {
        if (BLOCKS == null || BLOCKS.GetLength(0) == 0 || BLOCKS.GetLength(1) == 0)
        {
            BLOCKS = new Texture2D[5, 5];
        }
    }

    #region Kayıt
    public TextureCollection textureCollection;

    [InlineProperty]
    public Dictionary<Block.BlockType, AnimationCurve> blockDensityCurves;


    public void GenerateLevel(int levelNumber)
    {
        List<float> blockDensities = new List<float>();
        float totalDensity = 0;

        // BlockType ve AnimationCurve listelerini elde edelim
        List<Block.BlockType> blockTypes = new List<Block.BlockType>(blockDensityCurves.Keys);
        List<AnimationCurve> curves = new List<AnimationCurve>(blockDensityCurves.Values);

        // Curve'lerden yoğunluk değerlerini al ve toplamını bul
        for (int i = 0; i < curves.Count; i++)
        {
            float density = curves[i].Evaluate((float)levelNumber / 5000);
            blockDensities.Add(density);
            totalDensity += density;
        }

        // Yoğunluk değerlerini normalize et
        for (int i = 0; i < blockDensities.Count; i++)
        {
            blockDensities[i] /= totalDensity;
        }

        // Şimdi, her bir blok için normalize edilmiş yoğunluk değerlerini kullanarak matrise değer atama
        for (int i = 0; i < BLOCKS.GetLength(0); i++)
        {
            for (int j = 0; j < BLOCKS.GetLength(1); j++)
            {
                float randValue = UnityEngine.Random.value;
                float accumulatedDensity = 0;

                for (int blockType = 0; blockType < blockDensities.Count; blockType++)
                {
                    accumulatedDensity += blockDensities[blockType];

                    if (randValue <= accumulatedDensity)
                    {
                        BLOCKS[i, j] = IntToTexture(blockType);
                        break;
                    }
                }
            }
        }
    }


    Texture2D IntToTexture(int index) { return textureCollection.textures[index]; }
    int TextureToInt(Texture2D texture) { return textureCollection.textures.IndexOf(texture); }
    [Button("Regenerate Current Level", ButtonSizes.Medium)]
    public void RegenerateCurrentLevel()
    {
        GenerateLevel(levelNumber);
    }

    [Button("Regenerate All Levels", ButtonSizes.Medium)]
    public void RegenerateAllLevels() { for (int i = 1; i <= 5000; i++) { levelNumber = i; GenerateLevel(i); SaveLevelBlocks(); } }
    [Button("SAFE Regenerate All Levels", ButtonSizes.Medium)]
    public void SafeRegenerateAllLevels() { for (int i = 1; i <= 5000; i++) { SafeRegenerateCurrentLevel(i); SaveLevelBlocks(); } }


    #region Leveldaki Değişikliğe Göre CURVE GÜNCELLEMESİ
    public void UpdateCurvesBasedOnLevel()
    {
        Dictionary<int, float> blockRatios = CalculateBlockRatiosInCurrentLevel();

        foreach (var pair in blockRatios)
        {
            int blockType = pair.Key;
            float ratio = pair.Value;

            // İlgili curve'ü al ve bu level için değerini güncelle
            if (blockDensityCurves.ContainsKey((Block.BlockType)blockType))
            {
                blockDensityCurves[(Block.BlockType)blockType].AddKey((float)levelNumber / 5000, ratio);
            }
        }
    }

    private Dictionary<int, float> CalculateBlockRatiosInCurrentLevel()
    {
        Dictionary<int, int> blockCounts = new Dictionary<int, int>();
        int totalBlocks = 0;

        for (int i = 0; i < BLOCKS.GetLength(0); i++)
        {
            for (int j = 0; j < BLOCKS.GetLength(1); j++)
            {
                int blockType = TextureToInt(BLOCKS[i, j]);

                if (!blockCounts.ContainsKey(blockType))
                {
                    blockCounts[blockType] = 0;
                }

                blockCounts[blockType]++;
                totalBlocks++;
            }
        }

        Dictionary<int, float> blockRatios = new Dictionary<int, float>();

        foreach (var pair in blockCounts)
        {
            blockRatios[pair.Key] = (float)pair.Value / totalBlocks;
        }

        return blockRatios;
    }

    #endregion

    #region Safe Regenerate
    public void SafeRegenerateCurrentLevel(int levelNum)
    {
        levelNumber = levelNum;

        int[,] currentLevelBlocks = ConvertTexturesToInts();

        // Şu anki blok dağılımını al
        Dictionary<int, int> currentDistribution = GetCurrentBlockDistribution();

        // İdeal blok dağılımını hesapla
        Dictionary<int, int> idealDistribution = CalculateIdealBlockDistribution(levelNumber);

        // Değişiklikleri hesapla
        Dictionary<int, int> differences = new Dictionary<int, int>();

        foreach (var pair in idealDistribution)
        {
            differences[pair.Key] = pair.Value - currentDistribution.GetValueOrDefault(pair.Key, 0);
        }

        for (int i = 0; i < currentLevelBlocks.GetLength(0); i++)
        {
            for (int j = 0; j < currentLevelBlocks.GetLength(1); j++)
            {
                int currentBlockType = currentLevelBlocks[i, j];

                // Eğer anahtar mevcut değilse sözlüğe ekleyin
                if (!differences.ContainsKey(currentBlockType))
                {
                    differences[currentBlockType] = 0;
                }

                if (differences[currentBlockType] > 0)  // Bu kontrol, değerin sıfırın altına düşmesini engeller
                {
                    differences[currentBlockType]--;
                }
            }
        }

        // Farkları minimize ederek blokları değiştir
        AdjustBlocksBasedOnDifferences(differences);
    }

    private Dictionary<int, int> GetCurrentBlockDistribution()
    {
        Dictionary<int, int> distribution = new Dictionary<int, int>();

        // Her bir blok tipi için sayıyı hesapla
        // Örnek:
        // distribution[0] = tahta blok sayısı
        // distribution[1] = altın blok sayısı, vb.

        return distribution;
    }

    private Dictionary<int, int> CalculateIdealBlockDistribution(int levelNumber)
    {
        Dictionary<int, int> distribution = new Dictionary<int, int>();

        // Burada, belirli bir seviye numarası için ideal blok dağılımını hesaplayın
        // Bu, yoğunluk eğrilerinizi kullanarak yapılabilir

        return distribution;
    }
    private void AdjustBlocksBasedOnDifferences(Dictionary<int, int> differences)
    {
        for (int i = 0; i < BLOCKS.GetLength(0); i++)
        {
            for (int j = 0; j < BLOCKS.GetLength(1); j++)
            {
                int currentBlockType = TextureToInt(BLOCKS[i, j]);
                int idealBlockType = GetIdealBlockType(differences);  // Yeni bir fonksiyon ile ideal blok tipini döndürebiliriz

                if (currentBlockType != idealBlockType /*&& CanBeChanged(i, j)*/)  // CanBeChanged, bu bloğun değiştirilip değiştirilemeyeceğini kontrol eden başka bir fonksiyon olabilir
                {
                    BLOCKS[i, j] = IntToTexture(idealBlockType);
                    differences[currentBlockType]--;
                    differences[idealBlockType]++;
                }
            }
        }
    }

    private int GetIdealBlockType(Dictionary<int, int> differences)
    {
        // İdeal blok tipini, farkları minimize edecek şekilde belirleyin.
        // Örneğin, en büyük pozitif farka sahip blok tipini seçebilirsiniz.

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

        return idealBlockType;
    }

    #endregion

    #region Dosya İşlemleri
    string baseSavePath = "Assets/Database/Levels";  // Kaydetmek istediğiniz klasör yolu

    public int[,] ConvertTexturesToInts()
    {
        int[,] blocksInts = new int[BLOCKS.GetLength(0), BLOCKS.GetLength(1)];

        for (int i = 0; i < BLOCKS.GetLength(0); i++)
        {
            for (int j = 0; j < BLOCKS.GetLength(1); j++)
            {
                blocksInts[i, j] = TextureToInt(BLOCKS[i, j]);
            }
        }

        return blocksInts;
    }

    public void SaveLevelBlocks()
    {
        int[,] blocksInts = ConvertTexturesToInts();

        BinaryFormatter formatter = new BinaryFormatter();
        string path = $"{baseSavePath}/Level_{levelNumber}.dat";

        // Dizin kontrolü
        string directoryPath = Path.GetDirectoryName(path);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Hata ayıklama
        try
        {
            FileStream stream = new FileStream(path, FileMode.Create);
            formatter.Serialize(stream, blocksInts);
            stream.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while saving: " + ex.Message);
        }
    }



    public void LoadLevelBlocks()
    {
        string path = $"{baseSavePath}/Level_{levelNumber}.dat";

        if (File.Exists(path))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(path, FileMode.Open);

            int[,] blocksInts = formatter.Deserialize(stream) as int[,];
            stream.Close();

            // Convert int values back to Texture2D
            for (int i = 0; i < blocksInts.GetLength(0); i++)
            {
                for (int j = 0; j < blocksInts.GetLength(1); j++)
                {
                    BLOCKS[i, j] = IntToTexture(blocksInts[i, j]);
                }
            }
        }
        else
        {
            Debug.LogWarning("Save file not found for level " + levelNumber + ". Creating a new one.");
            GenerateLevel(levelNumber);
            SaveLevelBlocks();
            LoadLevelBlocks();
        }
    }

    #endregion
    #endregion
}
