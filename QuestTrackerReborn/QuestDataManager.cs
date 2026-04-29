using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json;

namespace QuestTrackerReborn
{
    class QuestDataManager
    {
        public IPluginLog pluginLog { get; private set; }

        private readonly Plugin plugin;
        
        private readonly Configuration configuration;

        public QuestDataManager(
            IDalamudPluginInterface pluginInterface, 
            IPluginLog pluginLog,
            Plugin plugin, 
            Configuration configuration)
        {
            this.pluginLog = pluginLog;
            this.plugin = plugin;
            this.configuration = configuration;
            
            try
            {
                pluginLog.Debug("Loading QuestData from data.json");
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("QuestTrackerReborn.data.json");
                using var stringStream = new StreamReader(stream);
                var jsonString = stringStream.ReadToEnd();
                plugin.QuestData = JsonConvert.DeserializeObject<QuestData>(jsonString);

                //QuestData quests;
//
                //foreach (var file in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                //{
                //    pluginLog.Debug(file);
                //    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(file);
                //    using var stringStream = new StreamReader(stream);
                //    var jsonString = stringStream.ReadToEnd();
                //    quests.Categories.Add(JsonConvert.DeserializeObject<QuestData>(jsonString));
                //}

                pluginLog.Debug("Load successful");
            }
            catch (Exception e)
            {
                pluginLog.Error("Error loading QuestData from data.json");
                pluginLog.Error(e.Message);
            }
        }
        
        private void DetermineStartArea()
        {
            configuration.StartArea = QuestManager.IsQuestComplete(65575) ? "Gridania" :
                                      QuestManager.IsQuestComplete(65643) ? "Limsa Lominsa" :
                                      QuestManager.IsQuestComplete(66130) ? "Ul'dah" : "";
            
            pluginLog.Debug($"Start Area {configuration.StartArea}");
        }

        private void DetermineGrandCompany()
        {
            configuration.GrandCompany = QuestManager.IsQuestComplete(66216) ? "Twin Adder" :
                                         QuestManager.IsQuestComplete(66217) ? "Maelstrom" :
                                         QuestManager.IsQuestComplete(66218) ? "Immortal Flames" : "";
            
            pluginLog.Debug($"Grand Company {configuration.GrandCompany}");
        }

        private void DetermineStartClass()
        {
            configuration.StartClass = (uint) (
                // Gladiator
                QuestManager.IsQuestComplete(65792) && !QuestManager.IsQuestComplete(65822) ? 65822 :
                // Pugilist
                QuestManager.IsQuestComplete(66090) && !QuestManager.IsQuestComplete(66089) ? 66089 :
                // Marauder
                QuestManager.IsQuestComplete(65849) && !QuestManager.IsQuestComplete(65848) ? 65848 :
                // Lancer
                QuestManager.IsQuestComplete(65583) && !QuestManager.IsQuestComplete(65754) ? 65754 :
                // Archer
                QuestManager.IsQuestComplete(65582) && !QuestManager.IsQuestComplete(65755) ? 65755 :
                // Rogue
                QuestManager.IsQuestComplete(65640) && !QuestManager.IsQuestComplete(65638) ? 65638 :
                // Conjurer
                QuestManager.IsQuestComplete(65584) && !QuestManager.IsQuestComplete(65747) ? 65747 :
                // Thaumaturge
                QuestManager.IsQuestComplete(65883) && !QuestManager.IsQuestComplete(65882) ? 65882 :
                // Arcanist
                QuestManager.IsQuestComplete(65991) && !QuestManager.IsQuestComplete(65990) ? 65990 : 0);
            
            pluginLog.Debug($"Start Class {configuration.StartClass}");
        }

        public void UpdateQuestData()
        {
            UpdateQuestData(plugin.QuestData);
        }

        private bool IsVariantComplete(Quest quest, int[] ids)
        {
            foreach (int id in ids)
            {
                if (quest.Id.Contains((uint)id))
                {
                    foreach (int other in ids)
                    {
                        if (other != id && QuestManager.IsQuestComplete((ushort)other))
                            return true;
                    }
                }
            }
            return false;
        }
        
        private void UpdateQuestData(QuestData questData)
        {
            questData.NumComplete = questData.Total = 0;
            if (configuration.StartArea == "") DetermineStartArea();
            if (configuration.GrandCompany == "") DetermineGrandCompany();
            if (configuration.StartClass == 0) DetermineStartClass();

            if (questData.Categories.Count > 0)
            {
                questData.Hide = true;
                foreach (var category in questData.Categories)
                {
                    UpdateQuestData(category);
                    questData.NumComplete += category.NumComplete;
                    questData.Total += category.Total;
                    if (!category.Hide) questData.Hide = false;
                }
            }
            else
            {
                questData.Hide = true;
                foreach (var quest in questData.Quests.ToList())
                {
                    if (!configuration.StartArea.IsNullOrEmpty() && !quest.Start.IsNullOrEmpty() && configuration.StartArea != quest.Start)
                    {
                        if (IsQuestComplete(quest))
                        {
                            pluginLog.Error($"Quest {quest.Title} {string.Join(" ", quest.Id)} is restricted but completed");
                        }

                        questData.Quests.Remove(quest);
                        continue;
                    }

                    if (!configuration.GrandCompany.IsNullOrEmpty() && !quest.Gc.IsNullOrEmpty() && configuration.GrandCompany != quest.Gc)
                    {
                        if (IsQuestComplete(quest))
                        {
                            pluginLog.Error($"Quest {quest.Title} {string.Join(" ", quest.Id)} is restricted but completed");
                        }

                        questData.Quests.Remove(quest);
                        continue;
                    }

                    if (configuration.StartClass != 0 && quest.Id.Contains(configuration.StartClass))
                    {
                        questData.Quests.Remove(quest);
                        continue;
                    }
                    
                    if (// ARR "Call of the Wild" Alliance Quests
                        IsVariantComplete(quest, [67001, 67002, 67003]) ||
                        // YorHa "Heads or Tails"
                        IsVariantComplete(quest, [69256, 69257]) ||
                        // Qitari "The First Stela"
                        IsVariantComplete(quest, [69336, 69337]) ||
                        // Qitari "The Second Stela"
                        IsVariantComplete(quest, [69338, 69339]) ||
                        // Qitari "The Third Stela"
                        IsVariantComplete(quest, [69340, 69341]) ||
                        // An Ill-conceived Venture
                        IsVariantComplete(quest, [66968, 66969, 66970])
                       )
                    {
                        questData.Quests.Remove(quest);
                    }
                    
                    if (IsQuestComplete(quest)) questData.NumComplete++;

                    quest.Hide = (configuration.DisplayOption == 1 && !IsQuestComplete(quest)) ||
                                 (configuration.DisplayOption == 2 && IsQuestComplete(quest));
                    if (!quest.Hide) questData.Hide = false;
                }

                questData.Total += questData.Quests.Count;
            }
        }

        public static bool IsQuestComplete(Quest quest)
        {
            foreach (var id in quest.Id)
            {
                if (QuestManager.IsQuestComplete(id)) return true;   
            }
            return false;
        }
    }
}
