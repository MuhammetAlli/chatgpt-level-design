using System.Collections;
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
    public int UniqueLevelCount = 50;

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

    private void OnLevelNumberChanged() { if (levelNumber > UniqueLevelCount) levelNumber = UniqueLevelCount; LoadLevelBlocks(); }

    [HorizontalGroup("Controls", 0.1f)]
    [Button(">", ButtonSizes.Small)]
    public void IncreaseLevel()
    {
        levelNumber++;
        if (levelNumber > UniqueLevelCount) levelNumber = UniqueLevelCount;
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
            float density = curves[i].Evaluate((float)levelNumber / (float)UniqueLevelCount);
            blockDensities.Add(density);
            totalDensity += density;
            Debug.Log($"Block Type {i}: Density = {density}, Total Density = {totalDensity}");
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


    Texture2D IntToTexture(int index) { /*Debug.Log("INT TO TEXTURE index: " + index);*/ return textureCollection.textures[index]; }
    int TextureToInt(Texture2D texture) { return textureCollection.textures.IndexOf(texture); }
    [Button("Regenerate Current Level", ButtonSizes.Medium)]
    public void RegenerateCurrentLevel()
    {
        GenerateLevel(levelNumber);
    }

    [Button("Regenerate All Levels", ButtonSizes.Medium)]
    public void RegenerateAllLevels() { for (int i = 1; i <= UniqueLevelCount; i++) { levelNumber = i; GenerateLevel(i); SaveLevelBlocks(); } }
    [Button("SAFE Regenerate All Levels", ButtonSizes.Medium)]
    public void SafeRegenerateAllLevels() { for (int i = 1; i <= UniqueLevelCount; i++) { SafeRegenerateCurrentLevel(i); SaveLevelBlocks(); } }


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
                blockDensityCurves[(Block.BlockType)blockType].AddKey((float)levelNumber / (float)UniqueLevelCount, ratio);
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
        int[,] currentLevelBlocks = ConvertTexturesToInts(levelNum);
        List<float> blockDensities = new List<float>();
        float totalDensity = 0;

        //   Debug.Log($"Level {levelNum}, Total Density Before: {totalDensity}");

        List<Block.BlockType> blockTypes = new List<Block.BlockType>(blockDensityCurves.Keys);
        List<AnimationCurve> curves = new List<AnimationCurve>(blockDensityCurves.Values);

        for (int i = 0; i < curves.Count; i++)
        {
            float density = curves[i].Evaluate((float)levelNum / (float)UniqueLevelCount);
            blockDensities.Add(density);
            totalDensity += density;

            //    Debug.Log($"Density: {density}, Total Density After: {totalDensity}");
        }

        Dictionary<int, int> currentDistribution = GetCurrentBlockDistribution();


        Dictionary<int, int> idealDistribution = CalculateIdealBlockDistribution(levelNum);


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


    private Dictionary<int, int> GetCurrentBlockDistribution()
    {
        Dictionary<int, int> distribution = new Dictionary<int, int>();

        for (int i = 0; i < BLOCKS.GetLength(0); i++)
        {
            for (int j = 0; j < BLOCKS.GetLength(1); j++)
            {
                int blockType = TextureToInt(BLOCKS[i, j]);

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


    private Dictionary<int, int> CalculateIdealBlockDistribution(int levelNumber)
    {
        Dictionary<int, int> distribution = new Dictionary<int, int>();

        List<float> blockDensities = new List<float>();
        float totalDensity = 0;

        // BlockType ve AnimationCurve listelerini elde edelim
        List<Block.BlockType> blockTypes = new List<Block.BlockType>(blockDensityCurves.Keys);
        List<AnimationCurve> curves = new List<AnimationCurve>(blockDensityCurves.Values);

        // Curve'lerden yoğunluk değerlerini al ve toplamını bul
        for (int i = 0; i < curves.Count; i++)
        {
            float density = curves[i].Evaluate((float)levelNumber / 50); // 50 ile bölme, test etmek için total level sayısını temsil ediyor.
            blockDensities.Add(density);
            totalDensity += density;
        }

        int totalBlocks = BLOCKS.GetLength(0) * BLOCKS.GetLength(1);

        for (int i = 0; i < blockDensities.Count; i++)
        {
            int idealCount = Mathf.RoundToInt(blockDensities[i] / totalDensity * totalBlocks);
            distribution[i] = idealCount;
        }

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
                Debug.Log("IDEALBLOCKTYPE index: " + idealBlockType);

                if (idealBlockType == -1)
                {
                    // Eğer ideal blok tipi bulunamıyorsa bu bloğu atla
                    continue;
                }

                if (currentBlockType != idealBlockType /*&& CanBeChanged(i, j)*/)  // CanBeChanged, bu bloğun değiştirilip değiştirilemeyeceğini kontrol eden başka bir fonksiyon olabilir
                {
                    BLOCKS[i, j] = IntToTexture(idealBlockType);
                    differences[currentBlockType]--;
                    differences[idealBlockType]++;
                }
            }
        }
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

    #region Dosya İşlemleri
    string baseSavePath = "Assets/Database/Levels";  // Kaydetmek istediğiniz klasör yolu

    public int[,] ConvertTexturesToInts(int levelNum)
    {
        // Level numarasını kullanarak kaydedilmiş dosyayı bulma
        string path = $"{baseSavePath}/Level_{levelNum}.dat";

        // Dosyanın var olup olmadığını kontrol etme
        if (!File.Exists(path))
        {
            Debug.LogError($"Level {levelNum} dosyası bulunamadı.");
            return null;
        }

        // Dosyayı okuma ve veriyi deserializasyon
        BinaryFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(path, FileMode.Open);
        int[,] blocksInts = formatter.Deserialize(stream) as int[,];
        stream.Close();

        if (blocksInts == null)
        {
            Debug.LogError($"Level {levelNum} verisi okunamadı ya da deserializasyon hatası.");
            return null;
        }

        return blocksInts;
    }


    public void SaveLevelBlocks()
    {
        int[,] blocksInts = ConvertTexturesToInts(levelNumber);

        BinaryFormatter formatter = new BinaryFormatter();
        string path = $"{baseSavePath}/Level_{levelNumber}.dat";
        //    Debug.Log($"File Path: {path}"); if (File.Exists(path)) Debug.Log("File exists."); else Debug.Log("File does not exist.");


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
        Debug.Log($"File Path: {path}"); if (File.Exists(path)) Debug.Log("File exists."); else Debug.Log("File does not exist.");


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
