using TagTool.Cache;
using TagTool.IO;
using System.Collections.Generic;
using System.IO;
using System;
using TagTool.Common;
using TagTool.Tags.Definitions;
using TagTool.Tags;
using TagTool.Serialization;
using TagTool.Bitmaps;
using TagTool.Tags.Resources;

namespace TagTool.Commands.Porting
{
    public class OpenMapFileCommand : Command
    {
        private HaloOnlineCacheContext CacheContext { get; }

        public OpenMapFileCommand(HaloOnlineCacheContext cacheContext)
            : base(false,

                  "OpenMapFile",
                  "Opens a map file.",

                  "OpenMapFile <Map File>",

                  "Opens a map file.")
        {
            CacheContext = cacheContext;
        }

        public override object Execute(List<string> args)
        {
            if (args.Count > 1)
                return false;
            string path = "";
            if (args.Count == 1)
                path = args[0];
            else
                path = @"C:\Users\Tiger\Desktop\halo online\maps\haloonline\guardian.map";
            var file = new FileInfo(path);

            GameCache cache = GameCache.Open(file);
            using(var stream = cache.TagCache.OpenTagCacheRead())
            {
                foreach (var tag in cache.TagCache.TagTable)
                {
                    if (tag.Group.Tag == "bitm")
                    {
                        var def = cache.Deserialize<Bitmap>(stream, tag);
                        byte[] bitmapData;
                        foreach (var res in def.Resources)
                            bitmapData = cache.ResourceCache.GetResourceData(res);
                    }
                    else if(tag.Group.Tag == "snd!")
                    {
                        var def = cache.Deserialize<Sound>(stream, tag);
                        byte[] soundData = cache.ResourceCache.GetResourceData(def.Resource);
                    }
                    else if(tag.Group.Tag == "jmad")
                    {
                        var def = cache.Deserialize<ModelAnimationGraph>(stream, tag);
                        byte[] jmadData;
                        foreach (var res in def.ResourceGroups)
                            jmadData = cache.ResourceCache.GetResourceData(res.ResourceReference);
                    }
                    else if(tag.Group.Tag == "mode")
                    {
                        var def = cache.Deserialize<RenderModel>(stream, tag);
                        byte[] modeData = cache.ResourceCache.GetResourceData(def.Geometry.Resource);
                    }
                    else if(tag.Group.Tag == "sbsp")
                    {
                        var def = cache.Deserialize<ScenarioStructureBsp>(stream, tag);
                        byte[] data;
                        if (def.Geometry.Resource.HaloOnlinePageableResource != null)
                            data = cache.ResourceCache.GetResourceData(def.Geometry.Resource);
                        if(def.Geometry2.Resource.HaloOnlinePageableResource != null)
                            data = cache.ResourceCache.GetResourceData(def.Geometry2.Resource);
                        if(def.PathfindingResource.HaloOnlinePageableResource != null)
                            data = cache.ResourceCache.GetResourceData(def.PathfindingResource);
                        if(def.CollisionBspResource.HaloOnlinePageableResource != null)
                            data = cache.ResourceCache.GetResourceData(def.CollisionBspResource);
                    }
                }
            }
            return true;
        }
    }
}

