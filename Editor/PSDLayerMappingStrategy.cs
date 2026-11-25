using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using PDNWrapper;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    internal interface IPSDLayerMappingStrategyComparable
    {
        int layerID
        {
            get;
        }

        string name
        {
            get;
        }

        bool isGroup
        {
            get;
        }
    }

    internal interface IPSDLayerMappingStrategy
    {
        bool Compare(IPSDLayerMappingStrategyComparable a, IPSDLayerMappingStrategyComparable b);
        bool Compare(IPSDLayerMappingStrategyComparable a, BitmapLayer b);
        string LayersUnique(IEnumerable<IPSDLayerMappingStrategyComparable> layers);
        GUID GenerateGUID(IPSDLayerMappingStrategyComparable layer);
        GUID GenerateGUID(BitmapLayer layer);
    }

    internal abstract class LayerMappingStrategy<T> : IPSDLayerMappingStrategy
    {
        string m_DuplicatedStringError = L10n.Tr("The following layers have duplicated identifier.");
        protected abstract T GetID(IPSDLayerMappingStrategyComparable layer);
        protected abstract T GetID(BitmapLayer layer);

        protected virtual bool IsGroup(IPSDLayerMappingStrategyComparable layer)
        {
            return layer.isGroup;
        }

        protected virtual bool IsGroup(BitmapLayer layer)
        {
            return layer.IsGroup;
        }

        public bool Compare(IPSDLayerMappingStrategyComparable x, BitmapLayer y)
        {
            return Comparer<T>.Default.Compare(GetID(x), GetID(y)) == 0 && IsGroup(x) == IsGroup(y);
        }

        public bool Compare(IPSDLayerMappingStrategyComparable x, IPSDLayerMappingStrategyComparable y)
        {
            return Comparer<T>.Default.Compare(GetID(x), GetID(y)) == 0 && IsGroup(x) == IsGroup(y);
        }

        public string LayersUnique(IEnumerable<IPSDLayerMappingStrategyComparable> layers)
        {
            HashSet<T> layerNameHash = new HashSet<T>();
            HashSet<T> layerGroupHash = new HashSet<T>();
            return LayersUnique(layers, layerNameHash, layerGroupHash);
        }

        public abstract GUID GenerateGUID(IPSDLayerMappingStrategyComparable layer);
        public abstract GUID GenerateGUID(BitmapLayer layer);

        string LayersUnique(IEnumerable<IPSDLayerMappingStrategyComparable> layers, HashSet<T> layerNameHash, HashSet<T> layerGroupHash)
        {
            List<string> duplicateLayerName = new List<string>();
            string duplicatedStringError = null;
            foreach (IPSDLayerMappingStrategyComparable layer in layers)
            {
                T id = GetID(layer);
                HashSet<T> hash = layer.isGroup ? layerGroupHash : layerNameHash;
                if (hash.Contains(id))
                    duplicateLayerName.Add(layer.name);
                else
                    hash.Add(id);
            }

            if (duplicateLayerName.Count > 0)
            {
                duplicatedStringError = m_DuplicatedStringError + "\n";
                duplicatedStringError += string.Join(", ", duplicateLayerName);
            }
            return duplicatedStringError;
        }
    }

    internal class LayerMappingUseLayerName : LayerMappingStrategy<string>
    {
        protected override string GetID(IPSDLayerMappingStrategyComparable x)
        {
            return $"{x.name.ToLower()} {x.isGroup}";
        }

        protected override string GetID(BitmapLayer x)
        {
            return $"{x.Name.ToLower()} {x.IsGroup}";
        }

        public override GUID GenerateGUID(IPSDLayerMappingStrategyComparable layer)
        {
            return StringToGUID(GetID(layer));
        }

        public override GUID GenerateGUID(BitmapLayer layer)
        {
            return StringToGUID(GetID(layer));
        }

        public static GUID StringToGUID(string input)
        {
            byte[] hashBytes = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(input));

            // Set version to 3 (MD5 hash-based UUID)
            hashBytes[6] = (byte)((hashBytes[6] & 0x0f) | 0x30);

            // Set variant to RFC 4122
            hashBytes[8] = (byte)((hashBytes[8] & 0x3f) | 0x80);

            string hexString = BitConverter.ToString(hashBytes).Replace("-", "");
            return new GUID(hexString);

        }

    }

    internal class LayerMappingUseLayerNameCaseSensitive : LayerMappingStrategy<string>
    {
        protected override string GetID(IPSDLayerMappingStrategyComparable x)
        {
            return $"{x.name} {x.isGroup}";
        }

        protected override string GetID(BitmapLayer x)
        {
            return $"{x.Name} {x.IsGroup}";
        }

        public override GUID GenerateGUID(IPSDLayerMappingStrategyComparable layer)
        {
            return LayerMappingUseLayerName.StringToGUID(GetID(layer));
        }

        public override GUID GenerateGUID(BitmapLayer layer)
        {
            return LayerMappingUseLayerName.StringToGUID(GetID(layer));
        }
    }

    internal class LayerMappingUserLayerID : LayerMappingStrategy<int>
    {
        protected override int GetID(IPSDLayerMappingStrategyComparable x)
        {
            return x.layerID;
        }

        protected override int GetID(BitmapLayer x)
        {
            return x.LayerID;
        }

        public override GUID GenerateGUID(IPSDLayerMappingStrategyComparable layer)
        {
            return GenerateGUID(layer.layerID);
        }

        static GUID GenerateGUID(int i)
        {
            if (i >= 0)
                ++i;
            GUID guid = new GUID((uint)i, 0, 0, 0);

            return guid;
        }

        public override GUID GenerateGUID(BitmapLayer layer)
        {
            return GenerateGUID(layer.LayerID);
        }
    }
}
