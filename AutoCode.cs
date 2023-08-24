using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
  [Info("Auto Code", "BlueBeka", "1.6.0")]
  [Description("Automatically sets the code on code locks when placed.")]
  public class AutoCode : RustPlugin
  {
    [PluginReference("NoEscape")]
    private Plugin pluginNoEscape;

    private AutoCodeConfig config;
    private Commands commands;
    private Data data;
    private Dictionary<BasePlayer, TempCodeLockInfo> tempCodeLocks;

    private const string HiddenCode = "****";

    #region Hooks

    private void Init()
    {
      config = new AutoCodeConfig(this);
      data = new Data(this);
      commands = new Commands(this);
      tempCodeLocks = new Dictionary<BasePlayer, TempCodeLockInfo>();

      config.Load();
      data.Load();
      Permissions.Register(this);
      commands.Register();
    }

    protected override void LoadDefaultConfig()
    {
      Interface.Oxide.LogInfo("New configuration file created.");
    }

    private void OnServerSave()
    {
      data.Save();
    }

    private void Unload()
    {
      RemoveAllTempCodeLocks();
      data.Save();
    }

    private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
    {
      // Not one of our temporary code locks?
      if (player == null || !tempCodeLocks.ContainsKey(player) || tempCodeLocks[player].CodeLock != codeLock)
      {
        UnsubscribeFromUnneededHooks();
        return null;
      }

      // Destroy the temporary code lock as soon as it's ok to do so.
      timer.In(0, () =>
      {
        DestroyTempCodeLock(player);
      });

      SetCode(player, code, tempCodeLocks[player].Guest);
      Effect.server.Run(codeLock.effectCodeChanged.resourcePath, player.transform.position);
      return false;
    }

    private void OnItemDeployed(Deployer deployer, BaseEntity lockable, CodeLock codeLock)
    {
      // Code already set?
      if (codeLock.hasCode && codeLock.hasGuestCode)
      {
        return;
      }

      BasePlayer player = BasePlayer.FindByID(codeLock.OwnerID);

      // No player or the player doesn't have permission?
      if (player == null || !permission.UserHasPermission(player.UserIDString, Permissions.Use))
      {
        return;
      }

      // No data for player.
      if (!data.Inst.playerSettings.ContainsKey(player.userID))
      {
        return;
      }

      // NoEscape blocked.
      if (pluginNoEscape != null)
      {
        if (
          config.Options.PluginIntegration.NoEscape.BlockRaid &&
          pluginNoEscape.Call<bool>("HasPerm", player.UserIDString, "raid.buildblock") &&
          pluginNoEscape.Call<bool>("IsRaidBlocked", player.UserIDString)
        )
        {
          Message(player, lang.GetMessage("NoEscape.RaidBlocked", this, player.UserIDString));
          return;
        }

        if (
          config.Options.PluginIntegration.NoEscape.BlockCombat &&
          pluginNoEscape.Call<bool>("HasPerm", player.UserIDString, "combat.buildblock") &&
          pluginNoEscape.Call<bool>("IsCombatBlocked", player.UserIDString)
        )
        {
          Message(player, lang.GetMessage("NoEscape.CombatBlocked", this, player.UserIDString));
          return;
        }
      }

      Data.Structure.PlayerSettings settings = data.Inst.playerSettings[player.userID];

      // Player doesn't have a code?
      if (settings == null || settings.code == null)
      {
        return;
      }

      // Set the main code.
      codeLock.code = settings.code;
      codeLock.hasCode = true;
      codeLock.whitelistPlayers.Add(player.userID);

      // Set the guest code.
      if (settings.guestCode != null)
      {
        codeLock.guestCode = settings.guestCode;
        codeLock.hasGuestCode = true;
        codeLock.guestPlayers.Add(player.userID);
      }

      // Lock the lock.
      codeLock.SetFlag(BaseEntity.Flags.Locked, true);

      if (!settings.quietMode)
      {
        Message(
          player,
          string.Format(
            lang.GetMessage(
              codeLock.hasGuestCode ? "CodeAutoLockedWithGuest" : "CodeAutoLocked",
              this,
              player.UserIDString),
            Formatter.Value(Utils.ShouldHideCode(player, settings) ? HiddenCode : codeLock.code),
            Formatter.Value(Utils.ShouldHideCode(player, settings) ? HiddenCode : codeLock.guestCode)
          )
        );
      }
    }

    private object CanUseLockedEntity(BasePlayer player, CodeLock codeLock)
    {
      // Is a player that has permission and lock is locked?
      if (
        player != null &&
        codeLock.hasCode &&
        codeLock.HasFlag(BaseEntity.Flags.Locked) &&
        permission.UserHasPermission(player.UserIDString, Permissions.Use) &&
        permission.UserHasPermission(player.UserIDString, Permissions.Try) &&
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
        { "Help", "Usage:\n{0}" },
        { "Description", "Automatically set the code on code locks you place." },
        { "Info", "Code: {0}\nGuest Code: {1}\nQuiet Mode: {2}" },
        { "NoPermission", "You don't have permission." },
        { "CodeAutoLocked", "Code lock placed with code {0}." },
        { "CodeAutoLockedWithGuest", "Code lock placed with code {0} and guest code {1}." },
        { "CodeUpdated", "Your auto-code has changed to {0}." },
        { "CodeUpdatedHidden", "New auto-code set." },
        { "GuestCodeUpdated", "Your guest auto-code has changed to {0}." },
        { "GuestCodeUpdatedHidden", "New guest auto-code set." },
        { "CodeRemoved", "Your auto-code has been removed." },
        { "GuestCodeRemoved", "Your guest auto-code has been removed." },
        { "InvalidArgsTooMany", "Too many arguments supplied." },
        { "NotSet", "Not set" },
        { "SyntaxError", "Syntax Error: expected command in the form:\n{0}" },
        { "SpamPrevention", "Too many recent auto-code sets. Please wait {0} and try again." },
        { "InvalidArguments", "Invalid arguments supplied." },
        { "ErrorNoPlayerFound", "Error: No player found." },
        { "ErrorMoreThanOnePlayerFound", "Error: More than one player found." },
        { "ResettingAllLockOuts", "Resetting lock outs for all players." },
        { "ResettingLockOut", "Resetting lock outs for {0}." },
        { "QuietModeEnable", "Quiet mode now enabled." },
        { "QuietModeDisable", "Quiet mode now disabled." },
        { "Enabled", "Enabled" },
        { "Disabled", "Disabled" },
        { "HelpExtendedCoreCommands", "Core Commands:" },
        { "HelpExtendedOtherCommands", "Other Commands:" },
        { "HelpExtendedInfo", "Show your settings:\n{0}" },
        { "HelpExtendedSetCode", "Set your auto-code to 1234:\n{0}" },
        { "HelpExtendedRandomCode", "Set your auto-code to a randomly generated code:\n{0}" },
        { "HelpExtendedRemoveCode", "Remove your set auto-code:\n{0}" },
        { "HelpExtendedCoreGuestCommands", "Each core command is also available in a guest code version. e.g.\n{0}" },
        { "HelpExtendedQuietMode", "Toggles on/off quiet mode:\n{0}" },
        { "HelpExtendedQuietModeDetails", "Less messages will be shown and your auto-code will be hidden." },
        { "HelpExtendedHelp", "Displays this help message:\n{0}" },
        { "NoLongerSupportedPick", "\"{0}\" is no longer supported, please use \"{1}\" instead." },
        { "NoEscape.RaidBlocked", "Auto-code disabled due to raid block." },
        { "NoEscape.CombatBlocked", "Auto-code disabled due to combat block." },
      }, this);
    }

    #endregion Hooks

    #region API

    [ObsoleteAttribute("This method is deprecated. Call GetCode instead.", false)]
    public string GetPlayerCode(BasePlayer player) => GetCode(player);

    /// <summary>
    /// Get the code for the given player.
    /// </summary>
    /// <param name="player">The player to get the code for.</param>
    /// <param name="guest">If true, the guest code will be returned instead of the main code.</param>
    /// <returns>A string of the player's code or null if the player doesn't have a code.</returns>
    public string GetCode(BasePlayer player, bool guest = false)
    {
      if (data.Inst.playerSettings.ContainsKey(player.userID))
      {
        return guest
          ? data.Inst.playerSettings[player.userID].guestCode
          : data.Inst.playerSettings[player.userID].code;
      }

      return null;
    }

    /// <summary>
    /// Set the code for the given player.
    /// </summary>
    /// <param name="player">The player to set the code for.</param>
    /// <param name="code">The code to set for the given player.</param>
    /// <param name="guest">If true, the guest code will be set instead of the main code.</param>
    /// <param name="quiet">If true, no output message will be shown to the player.</param>
    /// <param name="hideCode">If true, the new code won't be displayed to the user. Has no effect if quiet is true.</param>
    public void SetCode(BasePlayer player, string code, bool guest = false, bool quiet = false, bool hideCode = false)
    {
      if (!data.Inst.playerSettings.ContainsKey(player.userID))
      {
        data.Inst.playerSettings.Add(player.userID, new Data.Structure.PlayerSettings());
      }

      // Load the player's settings
      Data.Structure.PlayerSettings settings = data.Inst.playerSettings[player.userID];

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
          if (!quiet)
          {
            Message(
              player,
              string.Format(
                lang.GetMessage("SpamPrevention", this, player.UserIDString),
                TimeSpan.FromSeconds(Math.Ceiling(settings.lockedOutUntil - currentTime)).ToString(@"d\d\ h\h\ mm\m\ ss\s").TrimStart(' ', 'd', 'h', 'm', 's', '0'),
                config.Options.SpamPrevention.LockOutTime,
                config.Options.SpamPrevention.WindowTime
              )
            );
          }
          return;
        }

        // Haven't been locked out for a long time?
        if (currentTime > settings.lastLockedOut + config.Options.SpamPrevention.LockOutResetFactor * lockOutFor)
        {
          // Reset their lockOuts.
          settings.lockedOutTimes = 0;
        }
      }

      if (guest)
      {
        settings.guestCode = code;
      }
      else
      {
        settings.code = code;
      }

      settings.lastSet = currentTime;

      if (!quiet)
      {
        hideCode = hideCode || Utils.ShouldHideCode(player, settings);

        Message(
          player,
          hideCode
            ? lang.GetMessage(guest ? "GuestCodeUpdatedHidden" : "CodeUpdatedHidden", this, player.UserIDString)
            : string.Format(
                lang.GetMessage(guest ? "GuestCodeUpdated" : "CodeUpdated", this, player.UserIDString),
                Formatter.Value(code)
              )
        );
      }
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
    /// <param name="guest">If true, the guest code will be removed instead of the main code.</param>
    /// <param name="quiet">If true, no output message will be shown to the player.</param>
    public void RemoveCode(BasePlayer player, bool guest = false, bool quiet = false)
    {
      if (!data.Inst.playerSettings.ContainsKey(player.userID))
      {
        return;
      }

      // Load the player's settings.
      Data.Structure.PlayerSettings settings = data.Inst.playerSettings[player.userID];

      if (!guest)
      {
        settings.code = null;
      }

      // Remove the guest code both then removing the main code and when just removing the guest code.
      settings.guestCode = null;

      if (!quiet)
      {
        Message(
          player,
          lang.GetMessage(guest ? "GuestCodeRemoved" : "CodeRemoved", this, player.UserIDString)
        );
      }
    }

    [ObsoleteAttribute("This method is deprecated. Call IsValidCode instead.", false)]
    public bool ValidCode(string codeString) => ValidCode(codeString);

    /// <summary>
    /// Is the given string a valid code?
    /// </summary>
    /// <param name="code">The code to test.</param>
    /// <returns>True if it's valid, otherwise false.</returns>
    public bool IsValidCode(string codeString)
    {
      if (codeString == null)
      {
        return false;
      }

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
    /// Remove all lock outs caused by spam protection.
    /// </summary>
    /// <param name="player">The player to toggle quiet mode for.</param>
    /// <param name="quiet">If true, no output message will be shown to the player.</param>
    public void ToggleQuietMode(BasePlayer player, bool quiet = false)
    {
      // Load the player's settings.
      Data.Structure.PlayerSettings settings = data.Inst.playerSettings[player.userID];

      settings.quietMode = !settings.quietMode;

      if (!quiet)
      {
        if (settings.quietMode)
        {
          Message(
            player,
            string.Format(
              "{0}\n" + Formatter.SmallLineGap + "{1}",
              lang.GetMessage("QuietModeEnable", this, player.UserIDString),
              lang.GetMessage("HelpExtendedQuietModeDetails", this, player.UserIDString)
            )
          );
        }
        else
        {
          Message(
            player,
            lang.GetMessage("QuietModeDisable", this, player.UserIDString)
          );
        }
      }
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

    #endregion API

    /// <summary>
    /// Destroy the temporary code lock for the given player.
    /// </summary>
    /// <param name="player"></param>
    private void DestroyTempCodeLock(BasePlayer player)
    {
      // Code lock for player exists? Remove it.
      if (tempCodeLocks.ContainsKey(player))
      {
        // Code lock exists? Destroy it.
        if (!tempCodeLocks[player].CodeLock.IsDestroyed)
        {
          tempCodeLocks[player].CodeLock.Kill();
        }
        tempCodeLocks.Remove(player);
      }
      UnsubscribeFromUnneededHooks();
    }

    /// <summary>
    /// Remove all the temporary code locks.
    /// </summary>
    private void RemoveAllTempCodeLocks()
    {
      // Remove all temp code locks - we don't want to save them.
      foreach (TempCodeLockInfo codeLockInfo in tempCodeLocks.Values)
      {
        if (!codeLockInfo.CodeLock.IsDestroyed)
        {
          codeLockInfo.CodeLock.Kill();
        }
      }
      tempCodeLocks.Clear();
      UnsubscribeFromUnneededHooks();
    }

    /// <summary>
    /// Unsubscribe from things that there is not point currently being subscribed to.
    /// </summary>
    private void UnsubscribeFromUnneededHooks()
    {
      // No point listing for code lock codes if we aren't expecting any.
      if (tempCodeLocks.Count < 1)
      {
        Unsubscribe("OnCodeEntered");
      }
    }

    /// <summary>
    /// Message the given player via the in-game chat.
    /// </summary>
    private void Message(BasePlayer player, string message)
    {
      if (string.IsNullOrEmpty(message) || !player.IsConnected)
        return;

      player.SendConsoleCommand(
        "chat.add",
        2,
        config.Commands.ChatIconId,
        message
      );
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

      public AutoCodeConfig()
      {
      }

      public AutoCodeConfig(AutoCode plugin)
      {
        this.plugin = plugin;
        OxideConfig = plugin.Config;
      }

      public CommandsDef Commands = new CommandsDef();

      public class CommandsDef
      {
        public string Use = "code";
        public ulong ChatIconId = 76561199143387303u;
      };

      public OptionsDef Options = new OptionsDef();

      public class OptionsDef
      {
        public bool DisplayPermissionErrors = true;

        public SpamPreventionDef SpamPrevention = new SpamPreventionDef();

        public PluginIntegrationDef PluginIntegration = new PluginIntegrationDef();

        public class SpamPreventionDef
        {
          public bool Enabled = true;
          public int Attempts = 5;
          public double LockOutTime = 5.0;
          public double WindowTime = 30.0;
          public bool UseExponentialLockOutTime = true;
          public double LockOutResetFactor = 5.0;
        };

        public class PluginIntegrationDef
        {
          public NoEscapeDef NoEscape = new NoEscapeDef();

          public class NoEscapeDef
          {
            public bool BlockRaid = false;
            public bool BlockCombat = false;
          };
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

        // Plugin integration - No Escape.
        Options.PluginIntegration.NoEscape.BlockCombat = GetConfigValue(new string[] { "Options", "Plugin Integration", "No Escape", "Block Combat" }, false);
        Options.PluginIntegration.NoEscape.BlockRaid = GetConfigValue(new string[] { "Options", "Plugin Integration", "No Escape", "Block Raid" }, true);

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
      private readonly string Filename;

      // The actual data.
      public Structure Inst { private set; get; }

      public Data(AutoCode plugin)
      {
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
          public string guestCode = null;
          public bool quietMode = false;
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
    private static class Permissions
    {
      // Permissions.
      public const string Use = "autocode.use";

      public const string Try = "autocode.try";
      public const string Admin = "autocode.admin";

      /// <summary>
      /// Register the permissions.
      /// </summary>
      public static void Register(AutoCode plugin)
      {
        plugin.permission.RegisterPermission(Use, plugin);
        plugin.permission.RegisterPermission(Try, plugin);
        plugin.permission.RegisterPermission(Admin, plugin);
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

      public string Guest = "guest";
      public string PickCode = "pick";
      public string RandomCode = "random";
      public string RemoveCode = "remove";
      public string SetCode = "set";
      public string QuietMode = "quiet";
      public string Help = "help";

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
        if (!plugin.permission.UserHasPermission(player.UserIDString, Permissions.Admin))
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
        if (!plugin.permission.UserHasPermission(player.UserIDString, Permissions.Use))
        {
          if (plugin.config.Options.DisplayPermissionErrors)
          {
            plugin.Message(
              player,
              plugin.lang.GetMessage("NoPermission", plugin, player.UserIDString)
            );
          }
          return;
        }

        if (args.Length == 0)
        {
          ShowInfo(player, label, args);
          return;
        }

        // Create settings for user if they don't already have any settings.
        if (!plugin.data.Inst.playerSettings.ContainsKey(player.userID))
        {
          plugin.data.Inst.playerSettings.Add(player.userID, new Data.Structure.PlayerSettings());
        }

        int nextArg = 0;
        int argsRemainingCount = args.Length;

        string operation = args[nextArg++].ToLower();
        argsRemainingCount--;

        bool guest = false;

        if (operation == Guest)
        {
          if (argsRemainingCount < 1)
          {
            SyntaxError(player, label, args);
            return;
          }

          guest = true;
          operation = args[nextArg++].ToLower();
          argsRemainingCount--;
        }

        if (operation == SetCode)
        {
          if (argsRemainingCount < 1)
          {
            SyntaxError(player, label, args);
            return;
          }

          operation = args[nextArg++].ToLower();
          argsRemainingCount--;
        }

        // Help.
        if (operation == Help)
        {
          plugin.Message(player, GetHelpExtended(player, label));
          return;
        }

        // Pick code.
        if (operation == PickCode)
        {
          plugin.Message(
            player,
            string.Format(plugin.lang.GetMessage("NoLongerSupportedPick", plugin, player.UserIDString), PickCode, SetCode)
          );
          return;
        }

        // Toggle quiet mode.
        if (operation == QuietMode)
        {
          if (guest)
          {
            SyntaxError(player, label, args);
            return;
          }
          if (argsRemainingCount > 0)
          {
            plugin.Message(
              player,
              string.Format(plugin.lang.GetMessage("InvalidArgsTooMany", plugin, player.UserIDString), label)
            );
            return;
          }

          plugin.ToggleQuietMode(player);
          return;
        }

        // Remove?
        if (operation == RemoveCode)
        {
          if (argsRemainingCount > 0)
          {
            plugin.Message(
              player,
              string.Format(plugin.lang.GetMessage("InvalidArgsTooMany", plugin, player.UserIDString), label)
            );
            return;
          }

          plugin.RemoveCode(player, guest);
          return;
        }

        // Use random code?
        if (operation == RandomCode)
        {
          if (argsRemainingCount > 0)
          {
            plugin.Message(
              player,
              string.Format(plugin.lang.GetMessage("InvalidArgsTooMany", plugin, player.UserIDString), label)
            );
            return;
          }

          var hideCode = false;
          if (plugin.data.Inst.playerSettings.ContainsKey(player.userID))
          {
            Data.Structure.PlayerSettings settings = plugin.data.Inst.playerSettings[player.userID];
            hideCode = settings.quietMode;
          }

          plugin.SetCode(player, plugin.GenerateRandomCode(), guest, false, hideCode);
          return;
        }

        // Use given code?
        if (plugin.IsValidCode(operation))
        {
          if (argsRemainingCount > 0)
          {
            plugin.Message(
              player,
              string.Format(plugin.lang.GetMessage("InvalidArgsTooMany", plugin, player.UserIDString), label)
            );
            return;
          }

          plugin.SetCode(player, operation, guest);
          return;
        }

        SyntaxError(player, label, args);
      }

      /// <summary>
      /// Show the player their info.
      /// </summary>
      private void ShowInfo(BasePlayer player, string label, string[] args)
      {
        string code = null;
        string guestCode = null;
        bool quietMode = false;

        if (plugin.data.Inst.playerSettings.ContainsKey(player.userID))
        {
          Data.Structure.PlayerSettings settings = plugin.data.Inst.playerSettings[player.userID];
          code = settings.code;
          guestCode = settings.guestCode;
          quietMode = settings.quietMode;

          if (Utils.ShouldHideCode(player, settings))
          {
            code = code == null ? null : HiddenCode;
            guestCode = guestCode == null ? null : HiddenCode;
          }
        }

        plugin.Message(
          player,
          string.Format(
            "{0}\n" + Formatter.SmallLineGap + "{1}\n" + Formatter.SmallLineGap + "{2}",
            Formatter.H2(plugin.lang.GetMessage("Description", plugin, player.UserIDString)),
            string.Format(
              plugin.lang.GetMessage("Info", plugin, player.UserIDString),
              code != null ? Formatter.Value(code) : Formatter.NoValue(plugin.lang.GetMessage("NotSet", plugin, player.UserIDString)),
              guestCode != null ? Formatter.Value(guestCode) : Formatter.NoValue(plugin.lang.GetMessage("NotSet", plugin, player.UserIDString)),
              quietMode ? Formatter.Value(plugin.lang.GetMessage("Enabled", plugin, player.UserIDString)) : Formatter.NoValue(plugin.lang.GetMessage("Disabled", plugin, player.UserIDString))
            ),
            GetHelp(player, label)
          )
        );
      }

      /// <summary>
      /// Notify the player that they entered a syntax error in their "use" chat command.
      /// </summary>
      private void SyntaxError(BasePlayer player, string label, string[] args)
      {
        plugin.Message(
          player,
          string.Format(
            plugin.lang.GetMessage("SyntaxError", plugin, player.UserIDString),
            GetUsage(label)
          )
        );
      }

      /// <summary>
      /// Get help for the "use" command.
      /// </summary>
      public string GetHelp(BasePlayer player, string label)
      {
        return string.Format(
          plugin.lang.GetMessage("Help", plugin, player.UserIDString),
          GetUsage(label)
        );
      }

      /// <summary>
      /// Show how to use the "use" command.
      /// </summary>
      /// <returns></returns>
      private string GetUsage(string label)
      {
        return Formatter.Command(
          string.Format(
            "{0} [<{1}>]",
            label,
            string.Join("|", new string[] {
              string.Format(
                "[{0}] <{1}>",
                Guest,
                string.Join("|", new string[] {
                  string.Format(
                    "[{0}] {1}",
                    SetCode,
                    "1234"
                  ),
                  RandomCode,
                  RemoveCode
                })
              ),
              QuietMode,
              Help
            })
          )
        );
      }

      /// <summary>
      /// Display an extended help message to the player.
      /// </summary>
      public string GetHelpExtended(BasePlayer player, string label)
      {
        return string.Format(
          "{0}",
          string.Join(
            "\n" + Formatter.SmallLineGap,
            string.Join(
              "\n" + Formatter.SmallLineGap,
              Formatter.H2(plugin.lang.GetMessage("HelpExtendedCoreCommands", plugin, player.UserIDString)),
              Formatter.UL(
                string.Format(
                  plugin.lang.GetMessage("HelpExtendedInfo", plugin, player.UserIDString),
                  Formatter.Indent(Formatter.Command(string.Format("{0}", label)))
                ),
                string.Format(
                  plugin.lang.GetMessage("HelpExtendedSetCode", plugin, player.UserIDString),
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", label, "1234")))
                ),
                string.Format(
                  plugin.lang.GetMessage("HelpExtendedRandomCode", plugin, player.UserIDString),
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", label, RandomCode)))
                ),
                string.Format(
                  plugin.lang.GetMessage("HelpExtendedRemoveCode", plugin, player.UserIDString),
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", label, RemoveCode)))
                )
              ),
              string.Format(
                plugin.lang.GetMessage("HelpExtendedCoreGuestCommands", plugin, player.UserIDString),
                Formatter.Indent(Formatter.Command(string.Format("{0} {1} {2}", label, Guest, "5678")))
              )
            ),
            string.Join(
              "\n" + Formatter.SmallLineGap,
              Formatter.H2(plugin.lang.GetMessage("HelpExtendedOtherCommands", plugin, player.UserIDString)),
              Formatter.UL(
                string.Format(
                  plugin.lang.GetMessage("HelpExtendedQuietMode", plugin, player.UserIDString),
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", label, QuietMode)))
                ),
                string.Format(
                  plugin.lang.GetMessage("HelpExtendedHelp", plugin, player.UserIDString),
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", label, Help)))
                )
              )
            )
          )
        );
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

      /// <summary>
      /// Should the code for the given player be hidden in messages?
      /// </summary>
      public static bool ShouldHideCode(BasePlayer player, Data.Structure.PlayerSettings settings)
      {
        return settings.quietMode || player.net.connection.info.GetBool("global.streamermode");
      }
    }

    /// <summary>
    /// Text formatting functions for in-game chat.
    /// </summary>
    private static class Formatter
    {
      /// <summary>
      /// A line break in a very small font.
      /// </summary>
      public const string SmallestLineGap = "<size=6>\n</size>";

      /// <summary>
      /// A line break in a small font.
      /// </summary>
      public const string SmallLineGap = "<size=9>\n</size>";

      /// <summary>
      /// Format the given text as a header level 1.
      /// </summary>
      public static string H1(string text)
      {
        return string.Format("<size=20>{0}</size>", text);
      }

      /// <summary>
      /// Format the given text as a header level 2.
      /// </summary>
      public static string H2(string text)
      {
        return string.Format("<size=16>{0}</size>", text);
      }

      /// <summary>
      /// Small text.
      /// </summary>
      public static string Small(string text)
      {
        return string.Format("<size=12>{0}</size>", text);
      }

      /// <summary>
      /// Format the items as an unordered list.
      /// </summary>
      public static string UL(params string[] items)
      {
        return string.Join(
          "\n" + SmallestLineGap,
          items.Select(item => string.Format(" - {0}", item))
        );
      }

      /// <summary>
      /// Indent the given text.
      /// </summary>
      public static string Indent(string text)
      {
        return string.Format("    {0}", text);
      }

      /// <summary>
      /// Format the given text as a command.
      /// </summary>
      public static string Command(string text)
      {
        return string.Format("<color=#e0e0e0>{0}</color>", text);
      }

      public static string Value(string text)
      {
        return string.Format("<color=#bfff75>{0}</color>", text);
      }

      public static string NoValue(string text)
      {
        return string.Format("<color=#ff7771>{0}</color>", text);
      }
    }

    /// <summary>
    /// The data stored for temp code locks.
    /// </summary>
    private class TempCodeLockInfo
    {
      public readonly CodeLock CodeLock;
      public readonly bool Guest;

      public TempCodeLockInfo(CodeLock CodeLock, bool Guest = false)
      {
        this.CodeLock = CodeLock;
        this.Guest = Guest;
      }
    }
  }
}
