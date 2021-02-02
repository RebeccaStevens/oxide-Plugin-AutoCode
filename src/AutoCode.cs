using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Libraries;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
  [Info("Auto Code", "slaymaster3000", "0.0.0-development")]
  [Description("Automatically sets the code on code locks placed.")]
  class AutoCode : RustPlugin
  {
    private readonly Dictionary<BasePlayer, CodeLock> tempCodeLocks = new Dictionary<BasePlayer, CodeLock>();

    #region Hooks

    void Init()
    {
      AutoCodeConfig.Init(this);
      Data.Init(this);
      Permissions.Init(this);
      Commands.Init(this);
    }

    protected override void LoadDefaultConfig() {
      Interface.Oxide.LogInfo("New configuration file created.");
    }

    void OnServerSave()
    {
      Data.Save();

      // Remove all temp code locks - we don't want to save them.
      foreach (CodeLock codeLock in tempCodeLocks.Values)
      {
        if (!codeLock.IsDestroyed)
        {
          codeLock.Kill();
        }
      }
      tempCodeLocks.Clear();
      UnsubscribeFromUnneedHooks();
    }

    void OnServerShutdown()
    {
      Unload();
    }

    void Unload()
    {
      Data.Save();
    }

    void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
    {
      // Not one of our temporary code locks?
      if (player == null || !tempCodeLocks.ContainsKey(player) || tempCodeLocks[player] != codeLock)
      {
        UnsubscribeFromUnneedHooks();
        return;
      }

      DestoryTempCodeLock(player);
      UnsubscribeFromUnneedHooks();

      SetCode(player, code);
      Effect.server.Run(codeLock.effectCodeChanged.resourcePath, player.transform.position);
    }

    void OnEntitySpawned(CodeLock codeLock)
    {
      // Code already set?
      if (codeLock.hasCode)
      {
        return;
      }

      BasePlayer player = BasePlayer.FindByID(codeLock.OwnerID);

      // No player or the player doesn't have permission?
      if (player == null || !Permissions.Oxide.UserHasPermission(player.UserIDString, Permissions.Use))
      {
        return;
      }

      Data.Structure.PlayerSettings settings = Data.Inst.playerCodes[player.userID];

      // Disabled for the player or they haven't set a code?
      if (settings == null || !settings.enabled || settings.code == null)
      {
        return;
      }

      // Set code and lock the code lock.
      codeLock.code = settings.code;
      codeLock.hasCode = true;
      codeLock.whitelistPlayers.Add(player.userID);
      codeLock.SetFlag(BaseEntity.Flags.Locked, true);

      player.ChatMessage(
        string.Format(
          lang.GetMessage("CodeAutoLocked", this, player.UserIDString),
          player.net.connection.info.GetBool("global.streamermode") ? "****" : settings.code
        )
      );
    }

    object CanUseLockedEntity(BasePlayer player, CodeLock codeLock)
    {
      // Is a player that has permission and lock is locked?
      if (
        player != null &&
        codeLock.hasCode &&
        codeLock.HasFlag(BaseEntity.Flags.Locked) &&
        Permissions.Oxide.UserHasPermission(player.UserIDString, Permissions.Use) &&
        Permissions.Oxide.UserHasPermission(player.UserIDString, Permissions.Try)
      )
      {
        Data.Structure.PlayerSettings settings = Data.Inst.playerCodes[player.userID];

        // Player has plugin enabled and they have the code?
        if (settings != null && settings.enabled && codeLock.code == settings.code)
        {
          // Auth the player.
          codeLock.whitelistPlayers.Add(player.userID);
        }
      }

      // Use default behavior.
      return null;
    }

    protected override void LoadDefaultMessages()
    {
      lang.RegisterMessages(new Dictionary<string, string>
        {
          { "NoPermission", "You don't have permission." },
          { "Enabled", "Auto Code enabled." },
          { "Disabled", "Auto Code disabled." },
          { "CodeAutoLocked", "Code lock placed with code {0}." },
          { "CodeUpdated", "Your new code is {0}." },
          { "InvalidArgsTooMany", "No additional arguments expected." },
          { "SyntaxError", "Syntax Error: expected \"{0}\"" },
          { "SpamPrevention", "Too many recent code sets. Please wait {0} and try again." },
          { "InvalidArguments", "Invalid arguments supplied." },
          { "ErrorNoPlayerFound", "Error: No player found." },
          { "ErrorMoreThanOnePlayerFound", "Error: More than one player found." },
          { "ResettingAllLockOuts", "Resetting lock outs for all players." },
          { "ResettingLockOut", "Resetting lock outs for {0}." },
        }, this);
    }

    #endregion

    #region API

    /**
     * @deprecated Use `GetCode` instead.
     */
    public string GetPlayerCode(BasePlayer player) => GetCode(player);

    /**
     * Get the give player's code.
     *
     * If the player doesn't have a code or they have disabled this functionality, null is returned.
     */
    public string GetCode(BasePlayer player)
    {
      if (Data.Inst.playerCodes.ContainsKey(player.userID) && Data.Inst.playerCodes[player.userID].enabled)
      {
        return Data.Inst.playerCodes[player.userID].code;
      }

      return null;
    }

    /**
     * Set the code for the given player.
     */
    public void SetCode(BasePlayer player, string code)
    {
      if (!Data.Inst.playerCodes.ContainsKey(player.userID))
      {
        Data.Inst.playerCodes.Add(player.userID, new Data.Structure.PlayerSettings());
      }

      // Load the player's settings
      Data.Structure.PlayerSettings settings = Data.Inst.playerCodes[player.userID];

      if (settings == null)
      {
        Interface.Oxide.LogError(string.Format("No settings found for user \"{0}\" - setting should already be loaded.", player.displayName));
        return;
      }

      double currentTime = Utils.CurrentTime();

      if (AutoCodeConfig.SpamPreventionEnabled)
      {
        double timePassed = currentTime - settings.lastSet;
        bool lockedOut = currentTime < settings.lockedOutUntil;
        double lockOutFor = AutoCodeConfig.SpamLockOutTime * Math.Pow(2, (AutoCodeConfig.SpamLockOutTimeExponential ? settings.lockedOutTimes : 0));

        if (!lockedOut)
        {
          // Called again within spam window time?
          if (timePassed < AutoCodeConfig.SpamWindowTime)
          {
            settings.timesSetInSpamWindow++;
          }
          else
          {
            settings.timesSetInSpamWindow = 1;
          }

          // Too many recent changes?
          if (settings.timesSetInSpamWindow > AutoCodeConfig.SpamAttempts)
          {
            // Locked them out.
            settings.lockedOutUntil = currentTime + lockOutFor;
            settings.lastLockedOut = currentTime;
            settings.lockedOutTimes++;
            settings.timesSetInSpamWindow = 0;
            lockedOut = true;
          }
        }

        // Locked out?
        if (lockedOut)
        {
          player.ChatMessage(
            string.Format(
              lang.GetMessage("SpamPrevention", this, player.UserIDString),
              TimeSpan.FromSeconds(Math.Ceiling(settings.lockedOutUntil - currentTime)).ToString(@"d\d\ h\h\ mm\m\ ss\s").TrimStart(' ', 'd', 'h', 'm', 's', '0'),
              AutoCodeConfig.SpamLockOutTime,
              AutoCodeConfig.SpamWindowTime
            )
          );
          return;
        }

        // Haven't been locked out for a long time?
        if (currentTime > settings.lastLockedOut + AutoCodeConfig.SpamLockOutResetFactor * lockOutFor)
        {
          // Reset their lockOuts.
          settings.lockedOutTimes = 0;
        }
      }

      settings.code = code;
      settings.lastSet = currentTime;

      player.ChatMessage(
        string.Format(
          lang.GetMessage("CodeUpdated", this, player.UserIDString),
          player.net.connection.info.GetBool("global.streamermode") ? "****" : code
        )
      );
    }

    /**
     * Toggle enabled for the given player.
     */
    public void ToggleEnabled(BasePlayer player)
    {
      Data.Inst.playerCodes[player.userID].enabled = !Data.Inst.playerCodes[player.userID].enabled;
      player.ChatMessage(lang.GetMessage(Data.Inst.playerCodes[player.userID].enabled ? "Enabled" : "Disabled", this, player.UserIDString));
    }

    /**
     * Is the given string a valid code?
     */
    public bool ValidCode(string codeString)
    {
      int code;
      if (codeString.Length == 4 && int.TryParse(codeString, out code))
      {
        if (code >= 0 && code < 10000)
        {
          return true;
        }
      }

      return false;
    }

    /**
     * @deprecated Use `Generate` instead.
     */
    public static string GetRandomCode()
    {
      return Core.Random.Range(0, 10000).ToString("0000");
    }

    /**
     * Get a random code.
     */
    public string GenerateRandomCode()
    {
      return Core.Random.Range(0, 10000).ToString("0000");
    }

    /**
     * Open the code lock UI for the given player.
     */
    public void OpenCodeLockUI(BasePlayer player)
    {
      // Make sure any old code lock is destroyed.
      DestoryTempCodeLock(player);

      // Create a temporary code lock.
      CodeLock codeLock = GameManager.server.CreateEntity(
        "assets/prefabs/locks/keypad/lock.code.prefab",
        player.eyes.position + new Vector3(0, -3, 0)
      ) as CodeLock;

      // Creation failed? Exit.
      if (codeLock == null)
      {
        Interface.Oxide.LogError("Failed to create code lock.");
        return;
      }

      // Associate the lock with the player.
      tempCodeLocks.Add(player, codeLock);

      // Spawn and lock the code lock.
      codeLock.Spawn();
      codeLock.SetFlag(BaseEntity.Flags.Locked, true);

      // Open the code lock UI.
      codeLock.ClientRPCPlayer(null, player, "EnterUnlockCode");

      // Listen for code lock codes.
      Subscribe("OnCodeEntered");

      // Destroy the temporary code lock in 20s.
      timer.In(20f, () =>
      {
        if (tempCodeLocks.ContainsKey(player) && tempCodeLocks[player] == codeLock)
        {
          DestoryTempCodeLock(player);
        }
      });
    }

    /**
     * Destroy the temporary code lock for the given player.
     */
    public void DestoryTempCodeLock(BasePlayer player)
    {
      // Code lock for player exists? Remove it.
      if (tempCodeLocks.ContainsKey(player))
      {
        // Code lock exists? Destroy it.
        if (!tempCodeLocks[player].IsDestroyed)
        {
          tempCodeLocks[player].Kill();
        }
        tempCodeLocks.Remove(player);
      }
      UnsubscribeFromUnneedHooks();
    }

    /**
     * Reset (remove) all lock outs.
     */
    public void ResetAllLockOuts()
    {
      foreach (ulong userID in Data.Inst.playerCodes.Keys)
      {
        ResetLockOut(userID);
      }
    }

    /**
     * Reset (remove) the lock out for the given player.
     */
    public void ResetLockOut(BasePlayer player)
    {
      ResetLockOut(player.userID);
    }

    /**
     * Reset (remove) the lock out for the given user id.
     */
    public void ResetLockOut(ulong userID)
    {
      if (!Data.Inst.playerCodes.ContainsKey(userID))
      {
        return;
      }

      Data.Structure.PlayerSettings settings = Data.Inst.playerCodes[userID];
      settings.lockedOutTimes = 0;
      settings.lockedOutUntil = 0;
    }

    /**
     * Unsubscribe from things that there is not point currently being subscribed to.
     */
    public void UnsubscribeFromUnneedHooks()
    {
      // No point listing for code lock codes if we aren't expecting any.
      if (tempCodeLocks.Count < 1)
      {
        Unsubscribe("OnCodeEntered");
      }
    }

    #endregion

    /**
     * Everything related to permissions.
     */
    private static class Permissions
    {
      // The plugin.
      private static AutoCode AutoCode;

      // The oxide permission instance.
      public static Permission Oxide { private set; get; }

      // Permissions.
      public static string Use = "autocode.use";
      public static string Try = "autocode.try";

      public static void Init(AutoCode plugin)
      {
        AutoCode = plugin;
        Oxide = AutoCode.permission;
        Oxide.RegisterPermission(Use, AutoCode);
        Oxide.RegisterPermission(Try, AutoCode);
      }
    }

    /**
     * Everything related to commands.
     */
    private static class Commands
    {
      // The plugin.
      private static AutoCode AutoCode;

      // The rust command instance.
      public static Command Rust { private set; get; }

      // Console Commands.
      public static string ResetLockOut = "autocode.resetlockout";

      // Chat Commands.
      public static string Use = "code";

      // Chat Command Arguments.
      public static string PickCode = "pick";
      public static string RandomCode = "random";
      public static string ToggleEnabled = "toggle";

      public static void Init(AutoCode plugin)
      {
        AutoCode = plugin;
        Rust = AutoCode.cmd;
        Rust.AddConsoleCommand(ResetLockOut, AutoCode, HandleResetLockOut);
        Rust.AddChatCommand(Use, AutoCode, HandleUse);
      }

      /**
       * Reset lock out.
       */
      private static bool HandleResetLockOut(ConsoleSystem.Arg arg)
      {
        BasePlayer player = arg.Player();

        // Not admin?
        if (!arg.IsAdmin)
        {
          if (AutoCodeConfig.DisplayPermissionErrors)
          {
            arg.ReplyWith(AutoCode.lang.GetMessage("NoPermission", AutoCode, player?.UserIDString));
          }

          return false;
        }

        // Incorrect number of args given.
        if (!arg.HasArgs(1) || arg.HasArgs(2))
        {
          arg.ReplyWith(AutoCode.lang.GetMessage("InvalidArguments", AutoCode, player?.UserIDString));
          return false;
        }

        string resetForString = arg.GetString(0).ToLower();

        // Reset all?
        if (resetForString == "*")
        {
          arg.ReplyWith(AutoCode.lang.GetMessage("ResettingAllLockOuts", AutoCode, player?.UserIDString));
          AutoCode.ResetAllLockOuts();
          return true;
        }

        // Find the player to reset for.
        List<BasePlayer> resetForList = new List<BasePlayer>();
        foreach (BasePlayer p in BasePlayer.allPlayerList)
        {
          if (p == null || string.IsNullOrEmpty(p.displayName))
          {
            continue;
          }

          if (p.UserIDString == resetForString || p.displayName.Contains(resetForString, CompareOptions.OrdinalIgnoreCase))
          {
            resetForList.Add(p);
          }
        }

        // No player found?
        if (resetForList.Count == 0)
        {
          arg.ReplyWith(AutoCode.lang.GetMessage("ErrorNoPlayerFound", AutoCode, player?.UserIDString));
          return false;
        }

        // Too many players found?
        if (resetForList.Count > 1)
        {
          arg.ReplyWith(AutoCode.lang.GetMessage("ErrorMoreThanOnePlayerFound", AutoCode, player?.UserIDString));
          return false;
        }

        // Rest for player.
        arg.ReplyWith(
          string.Format(
            AutoCode.lang.GetMessage("ResettingLockOut", AutoCode, player?.UserIDString),
            resetForList[0].displayName
          )
        );
        AutoCode.ResetLockOut(resetForList[0]);
        return true;
      }

      /**
       * The "use" chat command.
       */
      private static void HandleUse(BasePlayer player, string label, string[] args)
      {
        // Allowed to use this command?
        if (!Permissions.Oxide.UserHasPermission(player.UserIDString, Permissions.Use))
        {
          if (AutoCodeConfig.DisplayPermissionErrors)
          {
            player.ChatMessage(AutoCode.lang.GetMessage("NoPermission", AutoCode, player.UserIDString));
          }
          return;
        }

        if (args.Length < 1)
        {
          SyntaxError(player, label, args);
          return;
        }

        // Create settings for user if they don't already have any settings.
        if (!Data.Inst.playerCodes.ContainsKey(player.userID))
        {
          Data.Inst.playerCodes.Add(player.userID, new Data.Structure.PlayerSettings());
        }

        string arg0 = args[0].ToLower();

        // Pick code.
        if (arg0 == PickCode)
        {
          if (args.Length > 1)
          {
            player.ChatMessage(string.Format(AutoCode.lang.GetMessage("InvalidArgsTooMany", AutoCode, player.UserIDString), label));
            return;
          }

          AutoCode.OpenCodeLockUI(player);
          return;
        }

        // Toggle enabled?
        if (arg0 == ToggleEnabled)
        {
          if (args.Length > 1)
          {
            player.ChatMessage(string.Format(AutoCode.lang.GetMessage("InvalidArgsTooMany", AutoCode, player.UserIDString), label));
            return;
          }

          AutoCode.ToggleEnabled(player);
          return;
        }

        // Use random code?
        if (arg0 == RandomCode)
        {
          if (args.Length > 1)
          {
            player.ChatMessage(string.Format(AutoCode.lang.GetMessage("InvalidArgsTooMany", AutoCode, player.UserIDString), label));
            return;
          }

          AutoCode.SetCode(player, AutoCode.GenerateRandomCode());
          return;
        }

        // Use given code?
        if (AutoCode.ValidCode(arg0))
        {
          if (args.Length > 1)
          {
            player.ChatMessage(string.Format(AutoCode.lang.GetMessage("InvalidArgsTooMany", AutoCode, player.UserIDString), label));
            return;
          }

          AutoCode.SetCode(player, arg0);
          return;
        }

        SyntaxError(player, label, args);
      }

      /**
       * Notify the player that they entered a syntax error in their "use" chat command.
       */
      private static void SyntaxError(BasePlayer player, string label, string[] args)
      {
        player.ChatMessage(
          string.Format(
            AutoCode.lang.GetMessage("SyntaxError", AutoCode, player.UserIDString),
            string.Format("/{0} {1}", label, HelpGetAllUseCommandArguments())
          )
        );
      }

      /**
       * Get all the arguments that can be supplied to the "use" command.
       */
      private static string HelpGetAllUseCommandArguments()
      {
        return string.Format("<{0}>", string.Join("|", new string[] { "1234", RandomCode, PickCode, ToggleEnabled }));
      }
    }

    /**
     * Everything related to the config.
     */
    private static class AutoCodeConfig
    {
      // The plugin.
      private static AutoCode AutoCode;

      // The oxide DynamicConfigFile instance.
      public static DynamicConfigFile OxideConfig { private set; get; }

      // Options.
      public static bool DisplayPermissionErrors { private set; get; }

      // Spam prevention.
      public static bool SpamPreventionEnabled { private set; get; }
      public static int SpamAttempts { private set; get; }
      public static double SpamLockOutTime { private set; get; }
      public static double SpamWindowTime { private set; get; }
      public static bool SpamLockOutTimeExponential { private set; get; }
      public static double SpamLockOutResetFactor { private set; get; }

      // Meta.
      private static bool UnsavedChanges = false;

      public static void Init(AutoCode plugin)
      {
        AutoCode = plugin;
        OxideConfig = AutoCode.Config;
        LoadConfigValues();
      }

      /**
       * Save the changes to the config file.
       */
      public static void Save(bool force = false)
      {
        if (UnsavedChanges || force)
        {
          AutoCode.SaveConfig();
        }
      }

      /**
       * Load config values.
       */
      public static void LoadConfigValues()
      {
        // Options.
        DisplayPermissionErrors = GetConfigValue(
          new string[] { "Options", "Display Permission Errors" },
          GetConfigValue(new string[] { "Options", "displayPermissionErrors" }, true, true)
        );
        RemoveConfigValue(new string[] { "Options", "displayPermissionErrors" }); // Remove deprecated version.

        // Spam prevention.
        SpamPreventionEnabled = GetConfigValue(new string[] { "Options", "Spam Prevention", "Enable" }, true);
        SpamAttempts = GetConfigValue(new string[] { "Options", "Spam Prevention", "Attempts" }, 5);
        SpamLockOutTime = GetConfigValue(new string[] { "Options", "Spam Prevention", "Lock Out Time" }, 5.0);
        SpamWindowTime = GetConfigValue(new string[] { "Options", "Spam Prevention", "Window Time" }, 30.0);
        SpamLockOutTimeExponential = GetConfigValue(new string[] { "Options", "Spam Prevention", "Exponential Lock Out Time" }, true);
        SpamLockOutResetFactor = GetConfigValue(new string[] { "Options", "Spam Prevention", "Lock Out Reset Factor" }, 5.0);

        // Commands.
        Commands.Use = GetConfigValue(new string[] { "Commands", "Use" }, Commands.Use);

        Save();
      }

      /**
       * Get the config value for the given settings.
       */
      private static T GetConfigValue<T>(string[] settingPath, T defaultValue, bool deprecated = false)
      {
        object value = OxideConfig.Get(settingPath);
        if (value == null)
        {
          if (!deprecated)
          {
            SetConfigValue(settingPath, defaultValue);
          }
          return defaultValue;
        }

        return OxideConfig.ConvertValue<T>(value);
      }

      /**
       * Set the config value for the given settings.
       */
      private static void SetConfigValue<T>(string[] settingPath, T newValue)
      {
        List<object> pathAndTrailingValue = new List<object>();
        foreach (var segment in settingPath)
        {
          pathAndTrailingValue.Add(segment);
        }
        pathAndTrailingValue.Add(newValue);

        OxideConfig.Set(pathAndTrailingValue.ToArray());
        UnsavedChanges = true;
      }

      /**
       * Remove the config value for the given setting.
       */
      private static void RemoveConfigValue(string[] settingPath)
      {
        if (settingPath.Length == 1)
        {
          OxideConfig.Remove(settingPath[0]);
          return;
        }

        List<string> parentPath = new List<string>();
        for (int i = 0; i < settingPath.Length - 1; i++)
        {
          parentPath.Add(settingPath[i]);
        }

        Dictionary<string, object> parent = OxideConfig.Get(parentPath.ToArray()) as Dictionary<string, object>;
        parent.Remove(settingPath[settingPath.Length - 1]);
      }
    }
    /**
     * Utility functions.
     */
    private static class Utils
    {
      public static double CurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
    }

    /**
     * Everything related to the data the plugin needs to save.
     */
    private static class Data
    {
      // The plugin.
      private static AutoCode AutoCode;

      // The plugin.
      private static string Filename;

      // The actual data.
      public static Structure Inst { private set; get; }

      public static void Init(AutoCode plugin)
      {
        AutoCode = plugin;
        Filename = AutoCode.Name;
        Inst = Interface.Oxide.DataFileSystem.ReadObject<Structure>(Filename);
      }

      /**
       * Save the data.
       */
      public static void Save()
      {
        Interface.Oxide.DataFileSystem.WriteObject(Filename, Inst);
      }

      /**
       * The data this plugin needs to save.
       */
      public class Structure
      {
        public Dictionary<ulong, PlayerSettings> playerCodes = new Dictionary<ulong, PlayerSettings>();

        /**
         * The settings saved for each player.
         */
        public class PlayerSettings
        {
          public string code = null;
          public bool enabled = true;
          public double lastSet = 0;
          public int timesSetInSpamWindow = 0;
          public double lockedOutUntil = 0;
          public double lastLockedOut = 0;
          public int lockedOutTimes = 0;
        }
      }
    }
  }
}
