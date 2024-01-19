using UnityEngine;
using System.IO;
using System;
using HarmonyLib;
using System.Collections.Generic;

public class ScatteredRemains : Mod
{
    Harmony harmony;
    static public JsonModInfo modInfo;
    static string configPath = Path.Combine(SaveAndLoad.WorldPath, "ScatteredRemains.json");
    public static JSONObject Config = getSaveJson();
    public static System.Random RNG = new System.Random();
    public static string[] YesNos = { "true", "false", "yes", "no", "1", "0" };
    public static float dropChance
    {
        get
        {
            if (Config.IsNull || !Config.HasField("chance"))
                return 1f;
            return Config.GetField("chance").n;
        }
        set
        {
            if (!Config.IsNull && Config.HasField("chance"))
                Config.SetField("chance", value);
            else
                Config.AddField("chance", value);
        }
    }
    public static bool alwaysSpecial
    {
        get
        {
            if (Config.IsNull || !Config.HasField("specialsAlwaysDrop"))
                return false;
            return Config.GetField("specialsAlwaysDrop").b;
        }
        set
        {
            if (!Config.IsNull && Config.HasField("specialsAlwaysDrop"))
                Config.SetField("specialsAlwaysDrop", value);
            else
                Config.AddField("specialsAlwaysDrop", value);
        }
    }

    public void Start()
    {
        modInfo = modlistEntry.jsonmodinfo;
        harmony = new Harmony("com.aidanamite.ScatteredRemains");
        harmony.PatchAll();
        Log("Mod has been loaded!");
    }

    public void OnModUnload()
    {
        harmony.UnpatchAll(harmony.Id);
        Debug.Log("Mod has been unloaded!");
    }

    public static void Log(object message)
    {
        Debug.Log("[" + modInfo.name + "]: " + message.ToString());
    }

    public static void ErrorLog(object message)
    {
        Debug.LogError("[" + modInfo.name + "]: " + message.ToString());
    }

    private static JSONObject getSaveJson()
    {
        JSONObject data;
        try
        {
            data = new JSONObject(File.ReadAllText(configPath));
        }
        catch
        {
            data = JSONObject.Create();
            saveJson(data);
        }
        return data;
    }

    private static void saveJson(JSONObject data)
    {
        try
        {
            File.WriteAllText(configPath, data.ToString());
        }
        catch (Exception err)
        {
            ErrorLog("An error occured while trying to save settings: " + err.Message);
        }
    }

    [ConsoleCommand(name: "setDeathDropChance", docs: "Syntax: 'setDeathDropChance <chance>'  Changes the chance of items dropping when you respawn. Use decimal values from 1 to 0. 1 is 100% chance")]
    public static string MyCommand(string[] args)
    {
        if (args.Length < 1)
            return "Not enough arguments";
        if (args.Length > 1)
            return "Too many arguments";
        try
        {
            dropChance = float.Parse(args[0]);
            saveJson(Config);
            return "Drop chance is now " + Math.Round(dropChance * 100) + "%";
        }
        catch
        {
            return "Cannot parse " + args[0] + " as a number";
        }
    }

    [ConsoleCommand(name: "setAlwaysDropSpecial", docs: "Syntax: 'setAlwaysDropSpecial <true|false>'  Sets weither or not \"special\" items such as equipment and placeables should be dropped regardless of drop chance")]
    public static string MyCommand2(string[] args)
    {
        if (args.Length < 1)
            return "Not enough arguments";
        if (args.Length > 1)
            return "Too many arguments";
        int responce = getYesNo(args[0]);
        if (responce == -1)
            return args[0] + " is not a valid input";
        alwaysSpecial = (new bool[] { true, false})[responce];
        saveJson(Config);
        return "Now special items will" + (alwaysSpecial ? "" : " not") + " always drop";
    }

    public static bool isItemSpecial(Network_Player player, ItemInstance item)
    {
        if (item.settings_buildable != null && item.settings_buildable.Placeable) return true;
        if (item.settings_equipment != null && item.settings_equipment.EquipType != EquipSlotType.None) return true;
        foreach (ItemConnection connect in player.PlayerItemManager.useItemController.allConnections)
            if (connect.inventoryItem == item.baseItem)
                return true;
        return false;
    }

    public static int getYesNo(string parameter)
    {
        parameter = parameter.ToLower();
        for (int i = 0;i < YesNos.Length;i++)
            if (YesNos[i].StartsWith(parameter))
                return i % 2;
        return -1;
    }

    public void ExtraSettingsAPI_SettingsOpen()
    {
        ExtraSettingsAPI_SetCheckboxState("Always Drop Special Items", alwaysSpecial);
        ExtraSettingsAPI_SetSliderValue("Drop Chance", dropChance);
    }

    public void ExtraSettingsAPI_SettingsClose()
    {
        alwaysSpecial = ExtraSettingsAPI_GetCheckboxState("Always Drop Special Items");
        dropChance = ExtraSettingsAPI_GetSliderRealValue("Drop Chance");
        saveJson(Config);
    }


    public bool ExtraSettingsAPI_GetCheckboxState(string SettingName) => false;
    public float ExtraSettingsAPI_GetSliderRealValue(string SettingName) => 0;
    public void ExtraSettingsAPI_SetCheckboxState(string SettingName, bool value) { }
    public void ExtraSettingsAPI_SetSliderValue(string SettingName, float value) { }

    public static bool TryDropItem(ItemInstance item)
    {
        if (!(alwaysSpecial && isItemSpecial(Patch_ResetSlot.player, item)))
        {
            int itemCount = item.Amount;
            int newCount = 0;
            for (int i = 0; i < itemCount; i++)
                if (RNG.NextDouble() < dropChance)
                    newCount++;
            if (newCount < itemCount)
            {
                if (newCount == 0)
                    item = null;
                else
                    item.Amount = newCount;
            }
        }
        if (item != null && item.Valid)
        {
            Helper.DropItem(item, Patch_ResetSlot.player.transform.position, new Vector3((float)RNG.NextDouble() - 0.5f, (float)RNG.NextDouble() - 0.5f, (float)RNG.NextDouble() - 0.5f), Patch_ResetSlot.player.PersonController.HasRaftAsParent);
            return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(PlayerInventory), "Clear")]
public class Patch_ClearInventory
{
    static void Prefix(Network_Player ___playerNetwork) => Patch_ResetSlot.player = ___playerNetwork;
    static void Postfix() => Patch_ResetSlot.player = null;
}

[HarmonyPatch(typeof(PlayerInventory), "ClearInventoryLeaveSome")]
public class Patch_PartialClearInventory
{
    static void Prefix(Network_Player ___playerNetwork) => Patch_ResetSlot.player = ___playerNetwork;
    static void Postfix() => Patch_ResetSlot.player = null;
}

[HarmonyPatch]
static class Patch_ResetSlot
{
    public static Network_Player player;

    [HarmonyPatch(typeof(Slot), "Reset")]
    [HarmonyPrefix]
    static void Reset(Slot __instance)
    {
        if (player && !__instance.IsEmpty)
            ScatteredRemains.TryDropItem(__instance.itemInstance);
    }

    [HarmonyPatch(typeof(Slot), "SetItem", typeof(Item_Base), typeof(int))]
    [HarmonyPrefix]
    static void SetItem(Slot __instance, int amount)
    {
        if (player && !__instance.IsEmpty)
            ScatteredRemains.TryDropItem(new ItemInstance(__instance.itemInstance.baseItem, __instance.itemInstance.Amount - amount, __instance.itemInstance.Uses, __instance.itemInstance.exclusiveString));
    }

    [HarmonyPatch(typeof(Slot), "SetUses")]
    [HarmonyPrefix]
    static bool SetUses(Slot __instance)
    {
        if (player && !__instance.IsEmpty && ScatteredRemains.TryDropItem(__instance.itemInstance))
        {
            __instance.SetItem(null);
            return false;
        }
        return true;
    }
}