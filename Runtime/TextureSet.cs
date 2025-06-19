using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Psd
{
    /// <summary>
    ///     A set of textures imported from a single PSD file. These may come from individual layers or flattened groups.
    ///     Each layer comes with a bounding rectangle over the full texture size.
    /// </summary>
    public class TextureSet : ScriptableObject
    {
        /// <summary>
        /// The name given to the first layer of a texture set, if we're importing a PSD with a single unnamed layer.
        /// </summary>
        public static string DEFAULT_NAME = "default";
        
        [Serializable]
        public struct Layer
        {
            /// <summary>Bounds of this sub layer within the broader document. In pixels. 0..size</summary>
            public Rect DocumentRect;

            /// <summary>The texture data for this sub layer.</summary>
            public Texture2D Texture;
        }

        /// <summary>Each layer imported from the PSD file.</summary>
        [FormerlySerializedAs("Textures")]
        public List<Layer> Layers = new();

        /// <summary>
        /// The size of the document, irrespective of the layers (some layers may be smaller, etc).
        /// </summary>
        public Vector2 DocumentSize;
    }
}
