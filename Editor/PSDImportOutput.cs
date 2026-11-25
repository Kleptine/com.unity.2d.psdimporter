#define ENABLE_2D_TILEMAP_EDITOR
#if ENABLE_2D_ANIMATION
using UnityEngine.U2D.Animation;
#endif
#if ENABLE_2D_TILEMAP_EDITOR
using UnityEngine.Tilemaps;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;


namespace UnityEditor.U2D.PSD
{
    record PSDImportOutput(ImportTextureWrapper textureGenerationOutput,
        IReadOnlyList<PSDLayer> psdLayers, IReadOnlyList<SpriteMetaData> spriteRects) : IDisposable
    {

        public void Dispose()
        {
            textureGenerationOutput?.Dispose();
            foreach (PSDLayer layer in psdLayers)
                layer?.Dispose();
        }

        public ImportTextureWrapper textureGenerationOutput { get; } = textureGenerationOutput;
        public IReadOnlyList<PSDLayer> psdLayers { get; } = psdLayers;
        public IReadOnlyList<SpriteMetaData> spriteRects { get; } = spriteRects;
    }

    record ProducedAsset
    {
        public Texture2D thumbNail { get; set; }
        public Texture2D texture { get; set; }
        public IReadOnlyList<Sprite> sprites { get; set; }
        public PSDImportData importData { get; set; }
        public GameObject prefabRoot { get; set; }
#if ENABLE_2D_ANIMATION
        public SkeletonAsset skeletonAsset { get; set; }
        public string skeletonAssetReferenceID { get; set; }
        public string skeletonAssetName { get; set; }
        public SpriteLibraryAsset spriteLibraryAsset { get; set; }
#endif

#if ENABLE_2D_TILEMAP_EDITOR
        public GameObject tilePaletteGO { get; set; }
        public IReadOnlyList<TileBase> tiles { get; set; }
        public GridPalette gridPalette { get; set; }
#endif
        // for backward compatibility, we will remove this in future
        public string textureAssetName { get; set; }
        public string spriteLibAssetName { get; set; }
        public string prefabAssetName { get; set; }
    }
}
