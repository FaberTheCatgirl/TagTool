﻿using System;
using System.Collections.Generic;
using System.IO;
using TagTool.Cache;
using TagTool.Bitmaps;
using TagTool.Tags;
using TagTool.Tags.Definitions;
using TagTool.Commands.Common;
using TagTool.Bitmaps.Utils;

namespace TagTool.Commands.Bitmaps
{
    public class GenerateMipmapsCommand : Command
    {
        private GameCache Cache { get; }
        private Bitmap Bitmap { get; }

        public GenerateMipmapsCommand(GameCache cache, Bitmap bitmap)
            : base(false,

                  "GenerateMipmaps",
                  "Generates mipmaps for the specified image. Removes any existing mips from the chain",

                  "GenerateMipmaps [image index]",
                  "Generates mipmaps for the specified image. Removes any existing mips from the chain")
        {
            Cache = cache;
            Bitmap = bitmap;
        }

        public override object Execute(List<string> args)
        {
            List<TagResourceReference> newHardwareTextures = new List<TagResourceReference>();
            if (args.Count > 0)
            {
                if (!int.TryParse(args[0], out int imageIndex) || imageIndex >= Bitmap.Images.Count)
                {
                    return new TagToolError(CommandError.ArgInvalid, $"\"{args[1]}\" is not a valid image index");
                }
                else
                {
                    newHardwareTextures.Add(DownsampleBitmapImage(imageIndex));
                }
            }
            else
            {
                for (int i = 0; i < Bitmap.Images.Count; i++)
                {
                    newHardwareTextures.Add(DownsampleBitmapImage(i));
                }
            }

            Bitmap.InterleavedHardwareTextures = new List<TagResourceReference>();
            Bitmap.HardwareTextures = newHardwareTextures;
            return true;
        }

        private TagResourceReference DownsampleBitmapImage(int imageIndex)
        {
            BitmapFormat currentFormat = Bitmap.Images[imageIndex].Format;
            var destFormat = currentFormat;

            var resourceReference = Bitmap.HardwareTextures[imageIndex];
            var resourceDefinition = Cache.ResourceCache.GetBitmapTextureInteropResource(resourceReference);

            byte[] primaryData = resourceDefinition?.Texture.Definition.PrimaryResourceData.Data;
            byte[] secondaryData = resourceDefinition?.Texture.Definition.SecondaryResourceData.Data;

            byte[] bitmapData;
            using (var result = new MemoryStream())
            {
                for (int layerIndex = 0; layerIndex < Bitmap.Images[imageIndex].Depth; layerIndex++)
                {
                    var pixelDataOffset = BitmapUtilsPC.GetTextureOffset(Bitmap.Images[imageIndex], 0);
                    var pixelDataSize = BitmapUtilsPC.GetMipmapPixelDataSize(Bitmap.Images[imageIndex], 0);

                    byte[] pixelData = new byte[pixelDataSize];
                    if (resourceDefinition.Texture.Definition.Bitmap.HighResInSecondaryResource > 0 || primaryData == null)
                    {
                        Array.Copy(secondaryData, pixelDataOffset, pixelData, 0, pixelData.Length);
                    }
                    else
                    {
                        if (secondaryData != null)
                            pixelDataOffset -= secondaryData.Length;
                        Array.Copy(primaryData, pixelDataOffset, pixelData, 0, pixelDataSize);
                    }

                    result.Write(pixelData, 0, pixelData.Length);
                }

                bitmapData = result.ToArray();
            }

            byte[] rawData = BitmapDecoder.DecodeBitmap(bitmapData, Bitmap.Images[imageIndex].Format, Bitmap.Images[imageIndex].Width, Bitmap.Images[imageIndex].Height);

            int lowestDimension = Math.Min(Bitmap.Images[imageIndex].Width, Bitmap.Images[imageIndex].Height);
            sbyte mipCount = (sbyte)Math.Log(lowestDimension, 2);
            BaseBitmap resultBitmap = new BaseBitmap(Bitmap.Images[imageIndex]);

            // this is unoptimized af, todo: clean it up

            byte[] mipData = new byte[rawData.Length];
            Buffer.BlockCopy(rawData, 0, mipData, 0, rawData.Length);
            int mipWidth = Bitmap.Images[imageIndex].Width, mipHeight = Bitmap.Images[imageIndex].Height;

            byte[] resultData = BitmapDecoder.EncodeBitmap(rawData, destFormat, resultBitmap.Width, resultBitmap.Height);

            int blockSize = BitmapFormatUtils.GetBlockSize(destFormat); // note: this can be -1

            for (int i = 0; i < mipCount; i++)
            {
                if (((mipWidth / 2) * (mipHeight / 2)) < blockSize) // limit mips for compressed formats
                {
                    mipCount = (sbyte)i; // already +1 from loop
                    break;
                }

                BitmapSampler sampler = new BitmapSampler(mipData, mipWidth, mipHeight, BitmapSampler.FilteringMode.Bilinear, 0);
                mipData = BitmapDownsampler.Downsample4x4BlockRgba(sampler);

                mipWidth /= 2; mipHeight /= 2;

                // generate mip and re-encode the bitmap to the specified format
                byte[] encodedMip = BitmapDecoder.EncodeBitmap(mipData, destFormat, mipWidth, mipHeight);

                int currentLength = resultData.Length;
                Array.Resize(ref resultData, currentLength + encodedMip.Length);
                Buffer.BlockCopy(encodedMip, 0, resultData, currentLength, encodedMip.Length);
            }


            // update image data

            resultBitmap.Data = resultData;
            Bitmap.Images[imageIndex].PixelDataOffset = 0;
            for (int i = 0; i < imageIndex; i++)
            {
                Bitmap.Images[imageIndex].PixelDataOffset += Bitmap.Images[i].PixelDataSize;
            }

            Bitmap.Images[imageIndex].PixelDataSize = resultBitmap.Data.Length;
            Bitmap.Images[imageIndex].MipmapCount = mipCount;

            // write to new resource

            var bitmapResourceDefinition = BitmapUtils.CreateBitmapTextureInteropResource(resultBitmap);
            var newResourceReference = Cache.ResourceCache.CreateBitmapResource(bitmapResourceDefinition);
            return newResourceReference;
        }
    }
}
