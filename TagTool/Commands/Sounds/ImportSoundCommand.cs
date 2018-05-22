﻿using TagTool.Cache;
using TagTool.Commands;
using TagTool.Common;
using TagTool.Serialization;
using TagTool.Tags.Definitions;
using TagTool.Tags.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using TagTool.Tags;
using TagTool.Audio;

namespace TagTool.Commands.Sounds
{
    class ImportSoundCommand : Command
    {
        private HaloOnlineCacheContext CacheContext { get; }
        private CachedTagInstance Tag { get; }
        private Sound Definition { get; }

        public ImportSoundCommand(HaloOnlineCacheContext cacheContext, CachedTagInstance tag, Sound definition) :
            base(true,
                
                "ImportSound",
                "Import a MP3 file into the current snd! tag. See documentation for formatting and options.",
                
                "ImportSound <Sound File>",
                "")
        {
            CacheContext = cacheContext;
            Tag = tag;
            Definition = definition;
        }

        public override object Execute(List<string> args)
        {
            if (args.Count != 1)
                return false;

            var resourceFile = new FileInfo(args[0]);
            var fileSize = 0;

            if (!resourceFile.Exists)
            {
                Console.WriteLine($"ERROR: File not found: \"{resourceFile.FullName}\"");
                return true;
            }

            //
            // Create new resource
            //

            Console.Write("Creating new sound resource...");
            
            Definition.Unknown12 = 0;
            
            using (var dataStream = resourceFile.OpenRead())
            {

                fileSize = (int)dataStream.Length;
                var resourceContext = new ResourceSerializationContext(Definition.Resource);
                CacheContext.Serializer.Serialize(resourceContext,
                    new SoundResourceDefinition
                    {
                        Data = new TagData(fileSize, new CacheAddress(CacheAddressType.Resource, 0))
                    });

                Definition.Resource = new PageableResource
                {
                    Page = new RawPage
                    {
                        Index = -1
                    },
                    Resource = new TagResource
                    {
                        Type = TagResourceType.Sound,
                        DefinitionData = new byte[20],
                        DefinitionAddress = new CacheAddress(CacheAddressType.Definition, 536870912),
                        ResourceFixups = new List<TagResource.ResourceFixup>
                        {
                            new TagResource.ResourceFixup
                            {
                                BlockOffset = 12,
                                Address = new CacheAddress(CacheAddressType.Resource, 1073741824)
                            }
                        },
                        ResourceDefinitionFixups = new List<TagResource.ResourceDefinitionFixup>(),
                        Unknown2 = 1
                    }
                };

                Definition.Resource.ChangeLocation(ResourceLocation.ResourcesB);
                CacheContext.AddResource(Definition.Resource, dataStream);
                
                for (int i = 0; i < 4; i++)
                {
                    Definition.Resource.Resource.DefinitionData[i] = (byte)(Definition.Resource.Page.UncompressedBlockSize >> (i * 8));
                }

                Console.WriteLine("done.");
            }

            //
            // Adjust tag definition to use correctly the sound file.
            //

            var chunkSize = (ushort)fileSize;
            
            var permutationChunk = new PermutationChunk
            {
                Offset = 0,
                Size = chunkSize,
                Unknown2 = (byte)((fileSize - chunkSize) / 65536),
                Unknown3 = 4,
                RuntimeIndex = -1,
                UnknownA = 0,
                UnknownSize = 0
            };

            var permutation = Definition.PitchRanges[0].Permutations[0];

            permutation.PermutationChunks = new List<PermutationChunk>
            {
                permutationChunk
            };

            permutation.PermutationNumber = 0;
            permutation.SampleSize = 0;
            permutation.IsNotFirstPermutation = 0;

            Definition.PitchRanges[0].Permutations = new List<Permutation>
            {
                permutation
            };

            Definition.PlatformCodec.Compression = Compression.MP3;
            
            return true;
        }
    }
}