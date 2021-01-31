using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Libraries;
using System.Collections.Generic;
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
      ChatCommands.Init(this);
    }

    protected override void LoadDefaultConfig() {
      Interface.Oxide.LogInfo("New configuration file created.");
    }

    void OnServerSave()
    {
      Data.Save();
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

    protected override void LoadDefaultMessages()
    {
      lang.RegisterMessages(new Dictionary<string, string>
        {
          {"NoPermission", "You don't have permission." },
          {"Enabled", "Auto Code enabled."},
          {"Disabled", "Auto Code disabled."},
          {"CodeAutoLocked", "Code lock placed with code {0}." },
          {"CodeUpdated", "Your new code is {0}." },
          {"InvalidArgsTooMany", "No additional arguments expected." },
          {"SyntaxError", "Syntax Error: expected \"{0}\"" },
        }, this);
    }

    #endregion

    #region API

    /**
     * Get the give player's code.
     *
     * If the player doesn't have a code or they have disabled this functionality, null is returned.
     */
    public string GetPlayerCode(BasePlayer player)
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

      settings.code = code;

      player.ChatMessage(
        string.Format(
          lang.GetMessage("CodeUpdated", this, player.UserIDString),
          player.net.connection.info.GetBool("global.streamermode") ? "****" : code)
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
     * Get a random code.
     */
    public static string GetRandomCode()
    {
      return Core.Random.Range(0, 10000).ToString("0000");
    }

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

      public static void Init(AutoCode plugin)
      {
        AutoCode = plugin;
        Oxide = AutoCode.permission;
        Oxide.RegisterPermission(Use, AutoCode);
      }
    }

    /**
     * Everything related to chat commands.
     */
    private static class ChatCommands
    {
      // The plugin.
      private static AutoCode AutoCode;

      // The rust command instance.
      public static Command Rust { private set; get; }

      // Commands.
      public static string Use = "code";

      // Arguments.
      public static string PickCode = "pick";
      public static string RandomCode = "random";
      public static string ToggleEnabled = "toggle";

      public static void Init(AutoCode plugin)
      {
        AutoCode = plugin;
        Rust = AutoCode.cmd;
        Rust.AddChatCommand(Use, AutoCode, Handle);
      }

      /**
       * The code chat command.
       */
      private static void Handle(BasePlayer player, string label, string[] args)
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

          AutoCode.SetCode(player, GetRandomCode());
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
        DisplayPermissionErrors = GetConfigValue(new string[] { "Options", "displayPermissionErrors" }, true);

        // Commands.
        ChatCommands.Use = GetConfigValue(new string[] { "Commands", "Use" }, ChatCommands.Use);

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
        }
      }
    }
  }
}

