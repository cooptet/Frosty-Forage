using UnityEngine;

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
#endif

namespace DuzeraTools.GradientAtlas
{
[CreateAssetMenu(fileName = "New GradientAtlasSO", menuName = "DuzeraTools/Gradient Atlas")]
public class GradientAtlasSO : ScriptableObject
{
#if UNITY_EDITOR
    public enum PowerOfTwoResolution
    {
        _8 = 8,
        _16 = 16,
        _32 = 32,
        _64 = 64,
        _128 = 128,
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
    }

    [Header("Atlas Settings")]
    [Tooltip("Resolution of the (square) atlas texture, in pixels.")]
    [SerializeField] private PowerOfTwoResolution _atlasResolution = PowerOfTwoResolution._256;
    [Tooltip("Width of each gradient cell, in pixels. Columns = atlas resolution / width.")]
    [SerializeField] private PowerOfTwoResolution _gradientWidth = PowerOfTwoResolution._32;
    [Tooltip("Height of each gradient cell, in pixels (colour runs bottom-to-top along Y). Rows = atlas resolution / height.")]
    [SerializeField] private PowerOfTwoResolution _gradientHeight = PowerOfTwoResolution._64;

    [Tooltip("When enabled, the atlas re-bakes automatically whenever a value changes — handy for live editing.")]
    [SerializeField] private bool _autoUpdate;

    [SerializeField, HideInInspector] private Gradient[] _gradients = new Gradient[0];

    [SerializeField, HideInInspector] private Texture2D _texture;
    [SerializeField, HideInInspector] private uint _bakedHash;

    private const int MaxGradientKeys = 8;
    private const float GradientFitTolerance = 1.5f / 255f;

    private bool _updateQueued;

    public Texture2D Texture => GetTexture();
    public Gradient[] Gradients => _gradients;

    public int AtlasResolution => (int)_atlasResolution;
    public int GradientWidth => Mathf.Min((int)_gradientWidth, AtlasResolution);
    public int GradientHeight => Mathf.Min((int)_gradientHeight, AtlasResolution);

    public int Columns => AtlasResolution / GradientWidth;
    public int Rows => AtlasResolution / GradientHeight;
    public int Count => Columns * Rows;

    public int BakedResolution => GetTexture() ? _texture.width : AtlasResolution;

    private string DefaultPngPath
    {
        get
        {
            string atlasPath = AssetDatabase.GetAssetPath(this);
            return string.IsNullOrEmpty(atlasPath) ? null : $"{Path.GetDirectoryName(atlasPath).Replace('\\', '/')}/{name.Replace(" ", "_")}_atlas.png";
        }
    }

    private void OnValidate()
    {
        ResizeSlots();

        if (_autoUpdate && !_updateQueued)
        {
            _updateQueued = true;
            EditorApplication.delayCall += DeferredAutoUpdate;
        }
    }

    private void DeferredAutoUpdate()
    {
        _updateQueued = false;

        if (this == null || !_autoUpdate)
            return;

        if (ComputeContentHash() == _bakedHash && OwnsPngTexture())
            return;

        UpdateAtlas();
    }

    private void ResizeSlots()
    {
        int expected = Count;
        if (_gradients == null || _gradients.Length != expected)
        {
            Gradient[] resized = new Gradient[expected];
            if (_gradients != null)
            {
                int copy = Mathf.Min(_gradients.Length, expected);
                for (int i = 0; i < copy; i++)
                    resized[i] = _gradients[i];
            }
            _gradients = resized;
        }
    }

    public Texture2D GetTexture()
    {
        if (!_texture && DefaultPngPath != null)
            _texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultPngPath);

        return _texture;
    }

    public RectInt GetPixelRect(int slotIndex) => GetPixelRect(slotIndex, BakedResolution);

    public Rect GetUVRect(int slotIndex)
    {
        int resolution = AtlasResolution;
        RectInt pixels = GetPixelRect(slotIndex, resolution);

        if (pixels.width == 0 || resolution == 0)
            return Rect.zero;

        return new Rect(
            pixels.x / (float)resolution,
            pixels.y / (float)resolution,
            pixels.width / (float)resolution,
            pixels.height / (float)resolution);
    }

    private RectInt GetPixelRect(int slotIndex, int resolution)
    {
        if (slotIndex < 0 || slotIndex >= Count)
            return new RectInt(0, 0, 0, 0);

        int row = slotIndex / Columns;
        int col = slotIndex % Columns;
        int rowFromBottom = Rows - 1 - row;

        int xStart = col * resolution / Columns;
        int xEnd = (col + 1) * resolution / Columns;
        int yStart = rowFromBottom * resolution / Rows;
        int yEnd = (rowFromBottom + 1) * resolution / Rows;

        return new RectInt(xStart, yStart, xEnd - xStart, yEnd - yStart);
    }

    public void UpdateAtlas()
    {
        if (!EditorUtility.IsPersistent(this))
        {
            Debug.LogWarning($"[GradientAtlas] '{name}': save the atlas asset before baking it.", this);
            return;
        }

        ResizeSlots();

        string pngPath = OwnsPngTexture() ? AssetDatabase.GetAssetPath(_texture) : AssetDatabase.GenerateUniqueAssetPath(DefaultPngPath);

        File.WriteAllBytes(Path.GetFullPath(pngPath), BakePng());

        bool isNewPng = AssetImporter.GetAtPath(pngPath) is not TextureImporter;

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        if (isNewPng && AssetImporter.GetAtPath(pngPath) is TextureImporter importer)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        _texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

        if (_texture.width < Columns || _texture.height < Rows)
            Debug.LogWarning($"[GradientAtlas] '{name}': the PNG's import Max Size {_texture.width} is too small for a " +
                             $"{Columns}x{Rows} grid — some cells collapse to zero pixels.", this);

        _bakedHash = ComputeContentHash();

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    private byte[] BakePng()
    {
        int resolution = AtlasResolution;

        Color[] pixels = new Color[resolution * resolution];

        for (int slotIndex = 0; slotIndex < _gradients.Length; slotIndex++)
        {
            Gradient gradient = _gradients[slotIndex];
            RectInt rect = GetPixelRect(slotIndex, resolution);

            for (int y = 0; y < rect.height; y++)
            {
                float t = rect.height > 1 ? y / (rect.height - 1.0f) : 0f;
                Color colour = gradient != null ? gradient.Evaluate(t) : Color.white;

                for (int x = 0; x < rect.width; x++)
                    pixels[(rect.x + x) + ((rect.y + y) * resolution)] = colour;
            }
        }

        Texture2D buffer = new(resolution, resolution, TextureFormat.RGBA32, false);
        buffer.SetPixels(pixels);
        buffer.Apply(false);

        byte[] png = buffer.EncodeToPNG();
        DestroyImmediate(buffer);

        return png;
    }

    public void ReadFromPng()
    {
        Texture2D png = GetTexture();
        if (!png || !OwnsPngTexture())
        {
            Debug.LogWarning($"[GradientAtlas] '{name}': no atlas PNG to read — generate the atlas first.", this);
            return;
        }

        Texture2D source = new(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes(Path.GetFullPath(AssetDatabase.GetAssetPath(png)))) || source.width != source.height)
        {
            Debug.LogWarning($"[GradientAtlas] '{name}': the atlas PNG must be a readable square image.", this);
            DestroyImmediate(source);
            return;
        }

        if (source.width != AtlasResolution)
            Debug.LogWarning($"[GradientAtlas] '{name}': the PNG is {source.width}px but the atlas is {AtlasResolution}px; " +
                             "cells are read proportionally.", this);

        Undo.RecordObject(this, "Read Gradient Atlas PNG");
        ResizeSlots();

        int resolution = source.width;
        Color[] pixels = source.GetPixels();
        DestroyImmediate(source);

        for (int slotIndex = 0; slotIndex < _gradients.Length; slotIndex++)
        {
            RectInt rect = GetPixelRect(slotIndex, resolution);
            if (rect.width == 0 || rect.height == 0)
                continue;

            Color[] samples = new Color[rect.height];
            for (int y = 0; y < rect.height; y++)
            {
                Color sum = Color.clear;
                for (int x = 0; x < rect.width; x++)
                    sum += pixels[(rect.x + x) + ((rect.y + y) * resolution)];

                samples[y] = sum / rect.width;
            }

            _gradients[slotIndex] = FitGradient(samples, _gradients[slotIndex]);
        }

        _bakedHash = ComputeContentHash();

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    private static Gradient FitGradient(Color[] samples, Gradient template)
    {
        Gradient gradient = new()
        {
            mode = template?.mode ?? GradientMode.Blend,
            colorSpace = template?.colorSpace ?? ColorSpace.Gamma
        };

        int last = samples.Length - 1;
        SortedSet<int> colorIndices = new() { 0, last };
        SortedSet<int> alphaIndices = new() { 0, last };

        while (true)
        {
            SetFittedKeys(gradient, samples, colorIndices, alphaIndices);

            int worstColor = -1, worstAlpha = -1;
            float colorError = GradientFitTolerance, alphaError = GradientFitTolerance;

            for (int i = 0; i <= last; i++)
            {
                Color fitted = gradient.Evaluate(SampleTime(i, samples.Length));
                Color target = samples[i];

                float rgbError = Mathf.Max(Mathf.Abs(fitted.r - target.r), Mathf.Abs(fitted.g - target.g), Mathf.Abs(fitted.b - target.b));
                if (rgbError > colorError && !colorIndices.Contains(i))
                {
                    colorError = rgbError;
                    worstColor = i;
                }

                float aError = Mathf.Abs(fitted.a - target.a);
                if (aError > alphaError && !alphaIndices.Contains(i))
                {
                    alphaError = aError;
                    worstAlpha = i;
                }
            }

            bool addColor = worstColor >= 0 && colorIndices.Count < MaxGradientKeys;
            bool addAlpha = worstAlpha >= 0 && alphaIndices.Count < MaxGradientKeys;

            if (!addColor && !addAlpha)
                return gradient;

            if (addColor)
                colorIndices.Add(worstColor);

            if (addAlpha)
                alphaIndices.Add(worstAlpha);
        }
    }

    private static void SetFittedKeys(Gradient gradient, Color[] samples, SortedSet<int> colorIndices, SortedSet<int> alphaIndices)
    {
        GradientColorKey[] colorKeys = new GradientColorKey[colorIndices.Count];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[alphaIndices.Count];

        int k = 0;
        foreach (int i in colorIndices)
        {
            Color c = samples[i];
            colorKeys[k++] = new GradientColorKey(new Color(c.r, c.g, c.b, 1f), SampleTime(i, samples.Length));
        }

        k = 0;
        foreach (int i in alphaIndices)
            alphaKeys[k++] = new GradientAlphaKey(samples[i].a, SampleTime(i, samples.Length));

        gradient.SetKeys(colorKeys, alphaKeys);
    }

    private static float SampleTime(int index, int count) => count > 1 ? index / (count - 1.0f) : 0f;

    private bool OwnsPngTexture()
    {
        if (!_texture || AssetDatabase.IsSubAsset(_texture))
            return false;

        string path = AssetDatabase.GetAssetPath(_texture);
        return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && AssetImporter.GetAtPath(path) is TextureImporter;
    }

    private uint ComputeContentHash()
    {
        uint hash = 2166136261u;

        Accumulate(ref hash, AtlasResolution);
        Accumulate(ref hash, GradientWidth);
        Accumulate(ref hash, GradientHeight);

        for (int slotIndex = 0; slotIndex < _gradients.Length; slotIndex++)
        {
            Gradient gradient = _gradients[slotIndex];

            if (gradient == null)
            {
                Accumulate(ref hash, 0);
                continue;
            }

            Accumulate(ref hash, (int)gradient.mode);
            Accumulate(ref hash, (int)gradient.colorSpace);

            foreach (GradientColorKey key in gradient.colorKeys)
            {
                Accumulate(ref hash, key.color.GetHashCode());
                Accumulate(ref hash, key.time.GetHashCode());
            }

            foreach (GradientAlphaKey key in gradient.alphaKeys)
            {
                Accumulate(ref hash, key.alpha.GetHashCode());
                Accumulate(ref hash, key.time.GetHashCode());
            }
        }

        return hash;
    }

    private static void Accumulate(ref uint hash, int value)
    {
        unchecked
        {
            for (int i = 0; i < 4; i++)
            {
                hash ^= (uint)((value >> (i * 8)) & 0xFF);
                hash *= 16777619u;
            }
        }
    }
#endif
}
}
