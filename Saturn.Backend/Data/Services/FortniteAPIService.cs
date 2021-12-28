﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Saturn.Backend.Data.Enums;
using Saturn.Backend.Data.Models.FortniteAPI;
using Saturn.Backend.Data.Models.Items;
using Saturn.Backend.Data.Utils;
using Serilog;

namespace Saturn.Backend.Data.Services
{
    public interface IFortniteAPIService
    {
        public Task<List<Cosmetic>> GetSaturnSkins();
        public Task<List<Cosmetic>> GetSaturnBackblings();
        public Task<List<Cosmetic>> GetSaturnDances();
        public Models.FortniteAPI.Data GetAES();
        public Task<List<Cosmetic>> AreItemsConverted(List<Cosmetic> items);
    }

    public class FortniteAPIService : IFortniteAPIService
    {
        private readonly IConfigService _configService;
        private readonly IDiscordRPCService _discordRPCService;
        private readonly ICloudStorageService _cloudStorageService;


        private readonly Uri Base = new("https://fortnite-api.com/v2/");


        public FortniteAPIService(IConfigService configService, IDiscordRPCService discordRPCService, ICloudStorageService cloudStorageService)
        {
            _configService = configService;
            _discordRPCService = discordRPCService;
            _cloudStorageService = cloudStorageService;
        }

        private Uri AES => new(Base, "aes");


        public Models.FortniteAPI.Data GetAES()
        {
            var data = GetData(AES);
            return JsonConvert.DeserializeObject<AES>(data).Data;
        }

        public async Task<List<Cosmetic>> GetSaturnDances()
        {
            var data = await GetDataAsync(CosmeticsByType("AthenaDance"));
            var Emotes = JsonConvert.DeserializeObject<CosmeticList>(data);
            Trace.WriteLine($"Deserialized {Emotes.Data.Count} objects");

            _discordRPCService.UpdatePresence($"Looking at {Emotes.Data.Count} different emotes");
            return await AreItemsConverted(Emotes.Data);
        }

        public async Task<List<Cosmetic>> GetSaturnBackblings()
        {
            var data = await GetDataAsync(CosmeticsByType("AthenaBackpack"));
            var Backs = JsonConvert.DeserializeObject<CosmeticList>(data);
            Trace.WriteLine($"Deserialized {Backs.Data.Count} objects");

            _discordRPCService.UpdatePresence($"Looking at {Backs.Data.Count} different backpacks");
            return await AreItemsConverted(Backs.Data);
        }

        public async Task<List<Cosmetic>> GetSaturnSkins()
        {
            var data = await GetDataAsync(CosmeticsByType("AthenaCharacter"));
            var Skins = JsonConvert.DeserializeObject<CosmeticList>(data);
            Trace.WriteLine($"Deserialized {Skins.Data.Count} objects");

            _discordRPCService.UpdatePresence($"Looking at {Skins.Data.Count} different skins");

            return await AreItemsConverted(await IsHatTypeDifferent(Skins.Data));
        }

        private async Task<List<Cosmetic>> IsHatTypeDifferent(List<Cosmetic> skins)
        {
            Logger.Log("Getting hat types");
            var DifferentHatsStr = _cloudStorageService.GetChanges("Skins", "HatTypes");

            Logger.Log("Decoding hat types");
            var DifferentHats = _cloudStorageService.DecodeChanges(DifferentHatsStr);
            
            foreach (var skin in skins.Where(skin => DifferentHats.HatSkins.IndexOf(skin.Id) != -1))
            {
                skin.HatTypes = HatTypes.HT_Hat;
                skin.CosmeticOptions = new List<SaturnItem>()
                {
                    new SaturnItem
                    {
                        ItemDefinition = "CID_162_Athena_Commando_F_StreetRacer",
                        Name = "Redline",
                        Description = "Revving beyond the limit.",
                        Icon =
                            "https://fortnite-api.com/images/cosmetics/br/cid_162_athena_commando_f_streetracer/smallicon.png",
                        Rarity = "Epic"
                    }
                };
            }
            
            return skins;
        }

        public async Task<List<Cosmetic>> AreItemsConverted(List<Cosmetic> items)
        {
            var ret = items;

            var convertedItems = await _configService.TryGetConvertedItems();
            convertedItems.Any(x => ret.Any(y =>
            {
                if (y.Id != x.ItemDefinition) return false;
                y.IsConverted = true;
                return true;
            }));

            return ret;
        }

        public string GetData(Uri uri)
        {
            using var wc = new WebClient();
            return wc.DownloadString(uri);
        }

        public async Task<string> GetDataAsync(Uri uri)
        {
            using var wc = new WebClient();
            return await wc.DownloadStringTaskAsync(uri);
        }

        private Uri CosmeticsByType(string type)
        {
            return new(Base, $"cosmetics/br/search/all?backendType={type}");
        }
    }
}