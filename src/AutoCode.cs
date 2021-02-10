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
    private AutoCodeConfig config;
    private Commands commands;
    private Data data;
    private Permissions permissions;
    private Dictionary<BasePlayer, CodeLock> tempCodeLocks;

    #region Hooks

    void Init()
    {
      config = new AutoCodeConfig(this);
      data = new Data(this);
      permissions = new Permissions(this);
      commands = new Commands(this);
      tempCodeLocks = new Dictionary<BasePlayer, CodeLock>();

      config.Load();
      data.Load();
      permissions.Register();
      commands.Register();
    }

    protected override void LoadDefaultConfig() {
      Interface.Oxide.LogInfo("New configuration file created.");
    }

    void OnServerSave()
    {
      data.Save();

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
      data.Save();
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
      if (player == null || !permissions.Oxide.UserHasPermission(player.UserIDString, permissions.Use))
      {
        return;
      }

      // No data for player.
      if (!data.Inst.playerSettings.ContainsKey(player.userID))
      {
        return;
      }

      Data.Structure.PlayerSettings settings = data.Inst.playerSettings[player.userID];

      // Player doesn't have a code?
      if (settings == null || settings.code == null)
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
        permissions.Oxide.UserHasPermission(player.UserIDString, permissions.Use) &&
        permissions.Oxide.UserHasPermission(player.UserIDString, permissions.Try) &&
        data.Inst.playerSettings.ContainsKey(player.userID)
      )
      {
        Data.Structure.PlayerSettings settings = data.Inst.playerSettings[player.userID];

        // Player has the code?
        if (settings != null && codeLock.code == settings.code)
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
          { "CodeAutoLocked", "Code lock placed with code {0}." },
          { "CodeUpdated", "Your new code is {0}." },
          { "CodeRemoved", "You're code has been removed." },
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

    [ObsoleteAttribute("This method is deprecated. Call GetCode instead.", false)]
    public string GetPlayerCode(BasePlayer player) => GetCode(player);

    /// <summary>
    /// Get the code for the given player.
    /// </summary>
    /// <param name="player">The player to get the code for.</param>
    /// <returns>A string of the player's code or null if the player doesn't have a code.</returns>
    public string GetCode(BasePlayer player)
    {
      if (data.Inst.playerSettings.ContainsKey(player.userID))
      {
        return data.Inst.playerSettings[player.userID].code;
      }

      return null;
    }

    /// <summary>
    /// Set the code for the given player.
    /// </summary>
    /// <param name="player">The player to set the code for.</param>
    /// <param name="code">The code to set for the given player.</param>
    public void SetCode(BasePlayer player, string code)
    {
      if (!data.Inst.playerSettings.ContainsKey(player.userID))
      {
        data.Inst.playerSettings.Add(player.userID, new Data.Structure.PlayerSettings());
      }

      // Load the player's settings
      Data.Structure.PlayerSettings settings = data.Inst.playerSettings[player.userID];

      if (settings == null)
      {
        Interface.Oxide.LogError(string.Format("No settings found for user \"{0}\" - setting should already be loaded.", player.displayName));
        return;
      }

      double currentTime = Utils.CurrentTime();

      if (config.Options.SpamPrevention.Enabled)
      {
        double timePassed = currentTime - settings.lastSet;
        bool lockedOut = currentTime < settings.lockedOutUntil;
        double lockOutFor = config.Options.SpamPrevention.LockOutTime * Math.Pow(2, (config.Options.SpamPrevention.UseExponentialLockOutTime ? settings.lockedOutTimes : 0));

        if (!lockedOut)
        {
          // Called again within spam window time?
          if (timePassed < config.Options.SpamPrevention.WindowTime)
          {
            settings.timesSetInSpamWindow++;
          }
          else
          {
            settings.timesSetInSpamWindow = 1;
          }

          // Too many recent changes?
          if (settings.timesSetInSpamWindow > config.Options.SpamPrevention.Attempts)
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
              config.Options.SpamPrevention.LockOutTime,
              config.Options.SpamPrevention.WindowTime
            )
          );
          return;
        }

        // Haven't been locked out for a long time?
        if (currentTime > settings.lastLockedOut + config.Options.SpamPrevention.LockOutResetFactor * lockOutFor)
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

    /// <summary>
    /// This method will only toggle off, not on.
    /// </summary>
    /// <param name="player"></param>
    [ObsoleteAttribute("This method is deprecated.", true)]
    public void ToggleEnabled(BasePlayer player)
    {
      RemoveCode(player);
    }

    /// <summary>
    /// Remove the given player's the code.
    /// </summary>
    /// <param name="player">The player to remove the code of.</param>
    public void RemoveCode(BasePlayer player)
    {
      if (!data.Inst.playerSettings.ContainsKey(player.userID))
      {
        return;
      }

      // Load the player's settings.
      Data.Structure.PlayerSettings settings = data.Inst.playerSettings[player.userID];

      settings.code = null;
      player.ChatMessage(lang.GetMessage("CodeRemoved", this, player.UserIDString));
    }

    /// <summary>
    /// Is the given string a valid code?
    /// </summary>
    /// <param name="code">The code to test.</param>
    /// <returns>True if it's valid, otherwise false.</returns>
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

    [ObsoleteAttribute("This method is deprecated. Call GenerateRandomCode instead.", false)]
    public static string GetRandomCode()
    {
      return Core.Random.Range(0, 10000).ToString("0000");
    }

    /// <summary>
    /// Generate a random code.
    /// </summary>
    /// <returns></returns>
    public string GenerateRandomCode()
    {
      return Core.Random.Range(0, 10000).ToString("0000");
    }

    /// <summary>
    /// Open the code lock UI for the given player.
    /// </summary>
    /// <param name="player">The player to open the lock UI for.</param>
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

    /// <summary>
    /// Reset (remove) all lock outs caused by spam protection.
    /// </summary>
    public void ResetAllLockOuts()
    {
      foreach (ulong userID in data.Inst.playerSettings.Keys)
      {
        ResetLockOut(userID);
      }
    }


    /// <summary>
    /// Reset (remove) the lock out caused by spam protection for the given player.
    /// </summary>
    /// <param name="player"></param>
    public void ResetLockOut(BasePlayer player)
    {
      ResetLockOut(player.userID);
    }

    /// <summary>
    /// Reset (remove) the lock out caused by spam protection for the given user id.
    /// </summary>
    /// <param name="userID"></param>
    public void ResetLockOut(ulong userID)
    {
      if (!data.Inst.playerSettings.ContainsKey(userID))
      {
        return;
      }

      Data.Structure.PlayerSettings settings = data.Inst.playerSettings[userID];
      settings.lockedOutTimes = 0;
      settings.lockedOutUntil = 0;
    }

    #endregion

    /// <summary>
    /// Destroy the temporary code lock for the given player.
    /// </summary>
    /// <param name="player"></param>
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

    /// <summary>
    /// Unsubscribe from things that there is not point currently being subscribed to.
    /// </summary>
    public void UnsubscribeFromUnneedHooks()
    {
      // No point listing for code lock codes if we aren't expecting any.
      if (tempCodeLocks.Count < 1)
      {
        Unsubscribe("OnCodeEntered");
      }
    }

    /// <summary>
    /// The Config for this plugin.
    /// </summary>
    private class AutoCodeConfig
    {
      // The plugin.
      private readonly AutoCode plugin;

      // The oxide DynamicConfigFile instance.
      public readonly DynamicConfigFile OxideConfig;

      // Meta.
      private bool UnsavedChanges = false;

      public AutoCodeConfig() { }

      public AutoCodeConfig(AutoCode plugin)
      {
        this.plugin = plugin;
        OxideConfig = plugin.Config;
      }

      public CommandsDef Commands = new CommandsDef();
      public class CommandsDef
      {
        public string Use = "code";
      };

      public OptionsDef Options = new OptionsDef();
      public class OptionsDef
      {
        public bool DisplayPermissionErrors = true;

        public SpamPreventionDef SpamPrevention = new SpamPreventionDef();
        public class SpamPreventionDef
        {
          public bool Enabled = true;
          public int Attempts = 5;
          public double LockOutTime = 5.0;
          public double WindowTime = 30.0;
          public bool UseExponentialLockOutTime = true;
          public double LockOutResetFactor = 5.0;
        };
      };

      /// <summary>
      /// Save the changes to the config file.
      /// </summary>
      public void Save(bool force = false)
      {
        if (UnsavedChanges || force)
        {
          plugin.SaveConfig();
        }
      }

      /// <summary>
      /// Load config values.
      /// </summary>
      public void Load()
      {
        // Options.
        Options.DisplayPermissionErrors = GetConfigValue(
          new string[] { "Options", "Display Permission Errors" },
          GetConfigValue(new string[] { "Options", "displayPermissionErrors" }, true, true)
        );
        RemoveConfigValue(new string[] { "Options", "displayPermissionErrors" }); // Remove deprecated version.

        // Spam prevention.
        Options.SpamPrevention.Enabled = GetConfigValue(new string[] { "Options", "Spam Prevention", "Enable" }, true);
        Options.SpamPrevention.Attempts = GetConfigValue(new string[] { "Options", "Spam Prevention", "Attempts" }, 5);
        Options.SpamPrevention.LockOutTime = GetConfigValue(new string[] { "Options", "Spam Prevention", "Lock Out Time" }, 5.0);
        Options.SpamPrevention.WindowTime = GetConfigValue(new string[] { "Options", "Spam Prevention", "Window Time" }, 30.0);
        Options.SpamPrevention.LockOutResetFactor = GetConfigValue(new string[] { "Options", "Spam Prevention", "Lock Out Reset Factor" }, 5.0);

        Options.SpamPrevention.UseExponentialLockOutTime = GetConfigValue(
          new string[] { "Options", "Spam Prevention", "Exponential Lock Out Time" },
          GetConfigValue(new string[] { "Options", "Spam Prevention", "Use Exponential Lock Out Time" }, true, true)
         );
        RemoveConfigValue(new string[] { "Options", "Spam Prevention", "Exponential Lock Out Time" }); // Remove deprecated version.

        // Commands.
        plugin.commands.Use = GetConfigValue(new string[] { "Commands", "Use" }, plugin.commands.Use);

        Save();
      }

      /// <summary>
      /// Get the config value for the given settings.
      /// </summary>
      private T GetConfigValue<T>(string[] settingPath, T defaultValue, bool deprecated = false)
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

      /// <summary>
      /// Set the config value for the given settings.
      /// </summary>
      private void SetConfigValue<T>(string[] settingPath, T newValue)
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

      /// <summary>
      /// Remove the config value for the given setting.
      /// </summary>
      private void RemoveConfigValue(string[] settingPath)
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

    /// <summary>
    /// Everything related to the data the plugin needs to save.
    /// </summary>
    private class Data
    {
      // The plugin.
      private readonly AutoCode plugin;

      // The plugin.
      private readonly string Filename;

      // The actual data.
      public Structure Inst { private set; get; }

      public Data(AutoCode plugin)
      {
        this.plugin = plugin;
        Filename = plugin.Name;
      }

      /// <summary>
      /// Save the data.
      /// </summary>
      public void Save()
      {
        Interface.Oxide.DataFileSystem.WriteObject(Filename, Inst);
      }

      /// <summary>
      /// Load the data.
      /// </summary>
      public void Load()
      {
        Inst = Interface.Oxide.DataFileSystem.ReadObject<Structure>(Filename);
      }

      /// <summary>
      /// The data this plugin needs to save.
      /// </summary>
      public class Structure
      {
        public Dictionary<ulong, PlayerSettings> playerSettings = new Dictionary<ulong, PlayerSettings>();

        /// <summary>
        /// The settings saved for each player.
        /// </summary>
        public class PlayerSettings
        {
          public string code = null;
          public double lastSet = 0;
          public int timesSetInSpamWindow = 0;
          public double lockedOutUntil = 0;
          public double lastLockedOut = 0;
          public int lockedOutTimes = 0;
        }
      }
    }

    /// <summary>
    /// The permissions this plugin uses.
    /// </summary>
    private class Permissions
    {
      // The plugin.
      private readonly AutoCode plugin;

      // The oxide permission instance.
      public readonly Permission Oxide;

      // Permissions.
      public string Use = "autocode.use";
      public string Try = "autocode.try";

      public Permissions(AutoCode plugin)
      {
        this.plugin = plugin;
        Oxide = plugin.permission;
      }

      /// <summary>
      /// Register the permissions.
      /// </summary>
      public void Register()
      {
        Oxide.RegisterPermission(Use, plugin);
        Oxide.RegisterPermission(Try, plugin);
      }
    }

    /// <summary>
    /// Everything related to commands.
    /// </summary>
    private class Commands
    {
      // The plugin.
      private readonly AutoCode plugin;

      // The rust command instance.
      public readonly Command Rust;

      // Console Commands.
      public string ResetLockOut = "autocode.resetlockout";

      // Chat Commands.
      public string Use = "code";

      // Chat Command Arguments.
      public string PickCode = "pick";
      public string RandomCode = "random";
      public string RemoveCode = "remove";

      public Commands(AutoCode plugin)
      {
        this.plugin = plugin;
        Rust = plugin.cmd;
      }

      /// <summary>
      /// Register this command.
      /// </summary>
      public void Register()
      {
        Rust.AddConsoleCommand(ResetLockOut, plugin, HandleResetLockOut);
        Rust.AddChatCommand(Use, plugin, HandleUse);
      }

      /// <summary>
      /// Reset lock out.
      /// </summary>
      /// <returns></returns>
      private bool HandleResetLockOut(ConsoleSystem.Arg arg)
      {
        BasePlayer player = arg.Player();

        // Not admin?
        if (!arg.IsAdmin)
        {
          if (plugin.config.Options.DisplayPermissionErrors)
          {
            arg.ReplyWith(plugin.lang.GetMessage("NoPermission", plugin, player?.UserIDString));
          }

          return false;
        }

        // Incorrect number of args given.
        if (!arg.HasArgs(1) || arg.HasArgs(2))
        {
          arg.ReplyWith(plugin.lang.GetMessage("InvalidArguments", plugin, player?.UserIDString));
          return false;
        }

        string resetForString = arg.GetString(0).ToLower();

        // Reset all?
        if (resetForString == "*")
        {
          arg.ReplyWith(plugin.lang.GetMessage("ResettingAllLockOuts", plugin, player?.UserIDString));
          plugin.ResetAllLockOuts();
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
          arg.ReplyWith(plugin.lang.GetMessage("ErrorNoPlayerFound", plugin, player?.UserIDString));
          return false;
        }

        // Too many players found?
        if (resetForList.Count > 1)
        {
          arg.ReplyWith(plugin.lang.GetMessage("ErrorMoreThanOnePlayerFound", plugin, player?.UserIDString));
          return false;
        }

        // Rest for player.
        arg.ReplyWith(
          string.Format(
            plugin.lang.GetMessage("ResettingLockOut", plugin, player?.UserIDString),
            resetForList[0].displayName
          )
        );
        plugin.ResetLockOut(resetForList[0]);
        return true;
      }

      /// <summary>
      /// The "use" chat command.
      /// </summary>
      private void HandleUse(BasePlayer player, string label, string[] args)
      {
        // Allowed to use this command?
        if (!plugin.permissions.Oxide.UserHasPermission(player.UserIDString, plugin.permissions.Use))
        {
          if (plugin.config.Options.DisplayPermissionErrors)
          {
            player.ChatMessage(plugin.lang.GetMessage("NoPermission", plugin, player.UserIDString));
          }
          return;
        }

        if (args.Length < 1)
        {
          SyntaxError(player, label, args);
          return;
        }

        // Create settings for user if they don't already have any settings.
        if (!plugin.data.Inst.playerSettings.ContainsKey(player.userID))
        {
          plugin.data.Inst.playerSettings.Add(player.userID, new Data.Structure.PlayerSettings());
        }

        string arg0 = args[0].ToLower();

        // Pick code.
        if (arg0 == PickCode)
        {
          if (args.Length > 1)
          {
            player.ChatMessage(string.Format(plugin.lang.GetMessage("InvalidArgsTooMany", plugin, player.UserIDString), label));
            return;
          }

          plugin.OpenCodeLockUI(player);
          return;
        }

        // Remove?
        if (arg0 == RemoveCode)
        {
          if (args.Length > 1)
          {
            player.ChatMessage(string.Format(plugin.lang.GetMessage("InvalidArgsTooMany", plugin, player.UserIDString), label));
            return;
          }

          plugin.RemoveCode(player);
          return;
        }

        // Use random code?
        if (arg0 == RandomCode)
        {
          if (args.Length > 1)
          {
            player.ChatMessage(string.Format(plugin.lang.GetMessage("InvalidArgsTooMany", plugin, player.UserIDString), label));
            return;
          }

          plugin.SetCode(player, plugin.GenerateRandomCode());
          return;
        }

        // Use given code?
        if (plugin.ValidCode(arg0))
        {
          if (args.Length > 1)
          {
            player.ChatMessage(string.Format(plugin.lang.GetMessage("InvalidArgsTooMany", plugin, player.UserIDString), label));
            return;
          }

          plugin.SetCode(player, arg0);
          return;
        }

        SyntaxError(player, label, args);
      }

      /// <summary>
      /// Notify the player that they entered a syntax error in their "use" chat command.
      /// </summary>
      private void SyntaxError(BasePlayer player, string label, string[] args)
      {
        player.ChatMessage(
          string.Format(
            plugin.lang.GetMessage("SyntaxError", plugin, player.UserIDString),
            string.Format("/{0} {1}", label, HelpGetAllUseCommandArguments())
          )
        );
      }

      /// <summary>
      /// Get all the arguments that can be supplied to the "use" command.
      /// </summary>
      /// <returns></returns>
      private string HelpGetAllUseCommandArguments()
      {
        return string.Format("<{0}>", string.Join("|", new string[] { "1234", RandomCode, PickCode, RemoveCode }));
      }
    }

    /// <summary>
    /// Utility functions.
    /// </summary>
    private static class Utils
    {
      /// <summary>
      /// Get the current time.
      /// </summary>
      /// <returns>The number of seconds that have passed since 1970-01-01.</returns>
      public static double CurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
    }
  }
}
