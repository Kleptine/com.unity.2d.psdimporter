/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop PSD FileType Plugin for Paint.NET
// http://psdplugin.codeplex.com/
//
// This software is provided under the MIT License:
//   Copyright (c) 2006-2007 Frank Blumenberg
//   Copyright (c) 2010-2016 Tao Yue
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using PDNWrapper;
using System.IO.Compression;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;

namespace PhotoshopFile.Compression
{
    internal class ZipPredict16Image : ImageData
    {
        private ImageData zipImage;

        protected override bool AltersWrittenData
        {
            get { return true; }
        }

        public ZipPredict16Image(byte[] zipData, Size size)
            : base(size, 16)
        {
            // 16-bitdepth images are delta-encoded word-by-word.  The deltas
            // are thus big-endian and must be reversed for further processing.
            var zipRawImage = new ZipImage(zipData, size, 16);
            zipImage = new EndianReverser(zipRawImage);
        }
        
        /// <summary>
        /// Job to unpredicts the decompressed, native-endian image data in parallel.
        /// </summary>
        [BurstCompile]
        private struct UnpredictJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<ushort> Data;
            [ReadOnly] public int Width;

            [BurstCompile]
            public void Execute(int i)
            {
                int rowOffset = Width * i;
                
                // Start with column index 1 on each row
                for (int j = 1; j < Width; ++j)
                {
                    var index = rowOffset + j;
                    Data[index] += Data[index - 1];
                }
            }
        }


        internal override void Read(byte[] buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            zipImage.Read(buffer);

            unsafe
            {
                // Wrap the managed byte array with a NativeArray without allocating new memory.
                // Reinterpret the byte array as a ushort array to work with 16-bit pixel data.
                fixed(void* bufferPtr = buffer)
                {
                    var nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(bufferPtr, buffer.Length, Allocator.None);
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                    var safetyHandle = AtomicSafetyHandle.Create();
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, safetyHandle);
                    #endif
                    var ushortArray = nativeArray.Reinterpret<ushort>(sizeof(byte));
                    
                    // Schedule and complete the job.
                    var job = new UnpredictJob
                    {
                        Data = ushortArray,
                        Width = Size.Width
                    };
                    job.Schedule(Size.Height, 1).Complete();
                    
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.Release(safetyHandle);
                    #endif
                }
            }
        }

        public override byte[] ReadCompressed()
        {
            return zipImage.ReadCompressed();
        }

        private void Predict(/*UInt16**/ byte[] ptrData)
        {
            int size = sizeof(UInt16);
            // Delta-encode each row
            for (int i = 0; i < Size.Height; i++)
            {
                int rowOffset = Size.Width * i * size;
                //UInt16* ptrDataRow = ptrData;
                int ptrDataRowEnd = Size.Width - 1;

                // Start with the last column in the row
                while (ptrDataRowEnd > 0)
                {
                    ushort v = BitConverter.ToUInt16(ptrData, ptrDataRowEnd * size + rowOffset);
                    ushort v1 = BitConverter.ToUInt16(ptrData, (ptrDataRowEnd - 1) * size + rowOffset);
                    v -= v1;
                    byte[] b = BitConverter.GetBytes(v);
                    for (int c = 0; c < b.Length; ++c)
                    {
                        ptrData[ptrDataRowEnd * size + rowOffset + c] = b[c];
                    }
                    ptrDataRowEnd--;
                }
            }
        }
    }
}