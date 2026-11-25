using System;
using System.Collections.Generic;
using System.IO;
using PDNWrapper;
using UnityEngine;
using Unity.Collections;
using Unity.Psd;
using UnityEditor.AssetImporters;
using UnityEditor.U2D.Common;

namespace UnityEditor.U2D.PSD
{
    public partial class PSDImporter
    {
        // Imports the file and separates out all layers or groups that are named with OUT_, flattening them.
        private void ImportAsTextureSet(AssetImportContext ctx, Document doc)
        {
            // We don't have a main texture in this mode, but we still need a main object.
            // We can use a simple GameObject as the main asset.
            var textureSet = ScriptableObject.CreateInstance<TextureSet>();
            textureSet.DocumentSize = new Vector2(doc.width, doc.height);
            textureSet.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            ctx.AddObjectToAsset("Main", textureSet);
            ctx.SetMainObject(textureSet);
            
            // If we're not separating out layers, just flatten all.
            if (!m_separateOutLayers)
            {
                using (PSDImportOutput flattened = ImportFlattenImage(doc, ctx, this))
                {
                    Texture2D flattenedTexture = flattened.textureGenerationOutput.GetTexture(ctx, this);
                    Texture2D flattenedThumbNail = flattened.textureGenerationOutput.GetThumbnail(ctx, this);
                    textureSet.Layers.Add(new TextureSet.Layer()
                    {
                        // DocumentRect = new Rect(rect.X, doc.height - rect.Y - rect.Height, rect.Width, rect.Height),
                        DocumentRect = new Rect(0,0,doc.width, doc.height),
                        Texture = flattenedTexture,
                    });
                    flattenedTexture.name = textureSet.name;
                    ctx.AddObjectToAsset(textureSet.name, flattenedTexture, flattenedThumbNail);
                }
                return;
            }

            // Use a helper to find all layers and groups that match the OUT or MASK prefix
            var layersToExport = FindExportableLayers(m_ExtractData);
            var layerNames = new HashSet<string>();

            foreach ((string layerType, PSDExtractLayerData layerData) in layersToExport)
            {
                NativeArray<Color32> imageData = default;

                try
                {
                    int width;
                    int height;
                    Rectangle rect;
                    if (layerData.bitmapLayer.IsGroup)
                    {
                        // For a group, flatten it into a single image
                        rect = GetFlattenedImageBounds(layerData.children, false);
                        width = doc.width;
                        height = doc.height;
                        imageData = new NativeArray<Color32>(width * height, Allocator.Persistent);
                        FlattenImageTask.Execute(layerData.children, ref imageData, m_ImportHiddenLayers, new Vector2Int(width, height));
                    }
                    else
                    {
                        // For a single layer, just use its texture data
                        var layer = layerData.bitmapLayer;
                        width = doc.width;
                        height = doc.height;
                        rect = layerData.bitmapLayer.documentRect;
                        if (layer.Surface.color.IsCreated && layer.Surface.color.Length > 0)
                        {
                            imageData = new NativeArray<Color32>(width * height, Allocator.Persistent);
                            FlattenImageTask.Execute(new []{ layerData }, ref imageData, m_ImportHiddenLayers, new Vector2Int(width, height));
                        }
                    }

                    if (imageData.IsCreated && imageData.Length > 0)
                    {
                        string textureName;
                        if (layerType == "OUT")
                        {
                            textureName = layerData.bitmapLayer.Name.Replace($"{layerType}_", "").Trim();

                            if (string.IsNullOrEmpty(textureName))
                            {
                                textureName = "Layer";
                                Debug.LogError(
                                    $"A layer has empty name [{layerData.bitmapLayer.Name}] which is invalid, using default name [{layerType}_Layer]",
                                    this);
                            }
                            if (layerNames.Contains(textureName))
                            {
                                Debug.LogError(
                                    $"More than one OUT_ layer with name: [{textureName}]. This is invalid. [{ctx.assetPath}]",
                                    this);
                            }
                        } else if (layerType == "MASK")
                        {
                            string maskName = layerData.bitmapLayer.Name.Replace($"{layerType}_", "").Trim();

                            if (string.IsNullOrEmpty(maskName))
                            {
                                maskName = "Layer";
                                Debug.LogError(
                                    $"A layer has empty name [{layerData.bitmapLayer.Name}] which is invalid, using default name [{layerType}_Layer]",
                                    this);
                            }

                            textureName = maskName + "_Mask";
                            if (layerNames.Contains(textureName))
                            {
                                Debug.LogError(
                                    $"More than one export layer with name: [{textureName}]. This is invalid. [{ctx.assetPath}]",
                                    this);
                            }
                        }
                        else
                        {
                            throw new Exception($"Unknown export layer type: {layerType}");
                        }
                        layerNames.Add(textureName);
                        
                        // Use a simplified sprite metadata for texture generation
                        SpriteMetaData[] spriteMetaData = { new SpriteMetaData { rect = new Rect(0, 0, width, height) } };
                        
                        // Generate the texture using the existing pipeline
                        Texture2D texture;
                        Texture2D thumbNail;
                        if (layerType == "OUT")
                        {
                            var textureWrapper = new ImportTextureWrapper(width, height, imageData, spriteMetaData);
                            texture = textureWrapper.GetTexture(ctx, this);
                            thumbNail = textureWrapper.GetThumbnail(ctx, this);
                        } else if (layerType == "MASK")
                        {
                            TextureImporterPlatformSettings platformSettings = TextureImporterUtilities.GetPlatformTextureSettings(ctx.selectedBuildTarget, in m_PlatformSettings);

                            TextureSettings textureSettings = m_TextureImporterSettings.ExtractTextureSettings();
                            textureSettings.assetPath = ctx.assetPath;
                            textureSettings.enablePostProcessor = true;
                            textureSettings.containsAlpha = true;
                            textureSettings.hdr = false;

                            TextureAlphaSettings textureAlphaSettings = m_TextureImporterSettings.ExtractTextureAlphaSettings();
                            TextureMipmapSettings textureMipmapSettings = m_TextureImporterSettings.ExtractTextureMipmapSettings();
                            TextureCubemapSettings textureCubemapSettings = m_TextureImporterSettings.ExtractTextureCubemapSettings();
                            TextureWrapSettings textureWrapSettings = m_TextureImporterSettings.ExtractTextureWrapSettings();
                            TextureGenerationOutput maskOutput = TextureGeneratorHelper.GenerateTextureSingleChannel(imageData, width, height,
                                textureSettings, platformSettings, textureAlphaSettings, textureMipmapSettings,
                                textureCubemapSettings, textureWrapSettings);
                            texture = maskOutput.texture;
                            thumbNail = maskOutput.thumbNail;
                        }
                        else
                        {
                            throw new Exception("Unknown export layer type: " + layerType);
                        }

                        if (texture != null)
                        {
                            // Add the generated texture as a sub-asset
                            texture.name = textureName;

                            List<TextureSet.Layer> texList = layerType == "OUT" ? textureSet.Layers : textureSet.Masks;

                            texList.Add(new TextureSet.Layer()
                            {
                                // DocumentRect = new Rect(rect.X, doc.height - rect.Y - rect.Height, rect.Width, rect.Height),
                                DocumentRect = new Rect(0,0,doc.width, doc.height),
                                Texture = texture,
                            });
                            ctx.AddObjectToAsset(textureName, texture, thumbNail);
                        }
                        else
                        {
                            throw new Exception("Failed to import texture for photoshop layer: " + layerData.bitmapLayer.Name);
                        }
                    }
                }
                finally
                {
                    if(imageData.IsCreated)
                        imageData.Dispose();
                }
            }
        }

        // Helper method to recursively find layers/groups with the "OUT_" or "MASK_" prefix
        private List<(string layerType, PSDExtractLayerData layerData)> FindExportableLayers(PSDExtractLayerData[] layers)
        {
            var exportableLayers = new List<(string,PSDExtractLayerData)>();
            if (layers == null)
                return exportableLayers;

            foreach (var layer in layers)
            {
                if (layer.bitmapLayer.Name.StartsWith("OUT_"))
                {
                    exportableLayers.Add(("OUT", layer));
                }
                else if (layer.bitmapLayer.Name.StartsWith("MASK_"))
                {
                    exportableLayers.Add(("MASK", layer));
                }
                else if (layer.children != null && layer.children.Length > 0)
                {
                    // If the parent is not an exportable group, check its children
                    exportableLayers.AddRange(FindExportableLayers(layer.children));
                }
            }
            return exportableLayers;
        }
        
        /// <summary>
        /// Calculates the combined bounding box of all layers that would be flattened.
        /// </summary>
        /// <param name="layers">The layer data to process.</param>
        /// <param name="importHiddenLayers">Should hidden layers be included in the calculation?</param>
        /// <returns>A Rectangle representing the union of all visible layer bounds.</returns>
        internal static Rectangle GetFlattenedImageBounds(PSDExtractLayerData[] layers, bool importHiddenLayers)
        {
            var visibleLayers = new List<PSDExtractLayerData>();
            CollectVisibleLayers(layers, importHiddenLayers, ref visibleLayers);

            if (visibleLayers.Count == 0)
            {
                return Rectangle.Empty;
            }

            // Start with the bounds of the first visible layer
            Rectangle mergedRect = visibleLayers[0].bitmapLayer.documentRect;

            // Union the bounds of all other visible layers
            for (int i = 1; i < visibleLayers.Count; i++)
            {
                mergedRect = Rectangle.Union(mergedRect, visibleLayers[i].bitmapLayer.documentRect);
            }

            return mergedRect;
        }

        /// <summary>
        /// Recursively collects all non-group layers that are visible and would be part of the flatten operation.
        /// </summary>
        private static void CollectVisibleLayers(PSDExtractLayerData[] layers, bool importHiddenLayers, ref List<PSDExtractLayerData> visibleLayers)
        {
            foreach (var layer in layers)
            {
                // A layer is processed if it's visible (or if we import hidden ones) and if its import setting is true.
                if ((layer.bitmapLayer.Visible || importHiddenLayers) && layer.importSetting.importLayer)
                {
                    if (layer.bitmapLayer.IsGroup)
                    {
                        if (layer.children != null)
                        {
                            CollectVisibleLayers(layer.children, importHiddenLayers, ref visibleLayers);
                        }
                    }
                    else
                    {
                        // Only add actual pixel layers to the list for bounds calculation
                        if (layer.bitmapLayer.Surface != null)
                        {
                            visibleLayers.Add(layer);
                        }
                    }
                }
            }
        }
    }
}
