using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
  [Info("Auto Code", "slaymaster3000", "0.0.0-development")]
  [Description("Automatically sets the code on code locks placed.")]
  class AutoCode : RustPlugin
  {
    // Permissions.
    private const string permissionUse = "autocode.use";

    // Commands.
    private string commandUse = "code";
    private string commandPickCode = "pick";
    private string commandCodeRandom = "random";
    private string commandToggleEnabled = "toggle";

    // Options
    private bool displayPermissionErrors = true;

    // Data.
    private readonly Dictionary<BasePlayer, CodeLock> tempCodeLocks = new Dictionary<BasePlayer, CodeLock>();
    private Data data;

    private bool configChanged = false;

    #region Hooks

    void Init()
    {
      LoadConfigValues();
      data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
      permission.RegisterPermission(permissionUse, this);
      cmd.AddChatCommand(commandUse, this, ChatCommand);
    }

    protected override void LoadDefaultConfig() {
      Interface.Oxide.LogInfo("New configuration file created.");
    }

    void OnServerSave()
    {
      SaveData();
    }

    void OnServerShutdown()
    {
      Unload();
    }

    void Unload()
    {
      SaveData();
    }

    void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
    {
      // Note one of our temporary code locks?
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

    void OnEntitySpawned(BaseNetworkable entity)
    {
      // Not a code lock?
      if (!(entity is CodeLock))
      {
        return;
      }

      CodeLock codeLock = entity as CodeLock;

      // Code already set?
      if (codeLock.hasCode)
      {
        return;
      }

      BasePlayer player = BasePlayer.FindByID(codeLock.OwnerID);

      // No player or the player doesn't have permission?
      if (player == null || !permission.UserHasPermission(player.UserIDString, permissionUse))
      {
        return;
      }

      PlayerSettings settings = data.playerCodes[player.userID];

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

    #region Commands

    /**
     * The code chat command.
     */
    private void ChatCommand(BasePlayer player, string Label, string[] Args)
    {
      // Allowed to use this command?
      if (!permission.UserHasPermission(player.UserIDString, permissionUse))
      {
        if (displayPermissionErrors)
        {
          player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
        }
        return;
      }

      if (Args.Length < 1)
      {
        SyntaxErrorChatCommand(player, Label, Args);
        return;
      }

      // Create settings for user if they don't already have any settings.
      if (!data.playerCodes.ContainsKey(player.userID))
      {
        data.playerCodes.Add(player.userID, new PlayerSettings());
      }

      string arg0 = Args[0].ToLower();

      // Pick code.
      if (arg0 == commandPickCode)
      {
        if (Args.Length > 1)
        {
          player.ChatMessage(string.Format(lang.GetMessage("InvalidArgsTooMany", this, player.UserIDString), Label));
          return;
        }

        OpenCodeLockUI(player);
        return;
      }

      // Toggle enabled?
      if (arg0 == commandToggleEnabled)
      {
        if (Args.Length > 1)
        {
          player.ChatMessage(string.Format(lang.GetMessage("InvalidArgsTooMany", this, player.UserIDString), Label));
          return;
        }

        ToggleEnabled(player);
        return;
      }

      // Use random code?
      if (arg0 == commandCodeRandom)
      {
        if (Args.Length > 1)
        {
          player.ChatMessage(string.Format(lang.GetMessage("InvalidArgsTooMany", this, player.UserIDString), Label));
          return;
        }

        SetCode(player, GetRandomCode());
        return;
      }

      // Use given code?
      if (ValidCodeString(arg0))
      {
        if (Args.Length > 1)
        {
          player.ChatMessage(string.Format(lang.GetMessage("InvalidArgsTooMany", this, player.UserIDString), Label));
          return;
        }

        SetCode(player, arg0);
        return;
      }

      SyntaxErrorChatCommand(player, Label, Args);
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
      if (data.playerCodes.ContainsKey(player.userID) && data.playerCodes[player.userID].enabled)
      {
        return data.playerCodes[player.userID].code;
      }

      return null;
    }

    /**
     * Set the code for the given player.
     */
    public void SetCode(BasePlayer player, string code)
    {
      if (!data.playerCodes.ContainsKey(player.userID))
      {
        data.playerCodes.Add(player.userID, new PlayerSettings());
      }

      // Load the player's settings
      PlayerSettings settings = data.playerCodes[player.userID];

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
      data.playerCodes[player.userID].enabled = !data.playerCodes[player.userID].enabled;
      player.ChatMessage(lang.GetMessage(data.playerCodes[player.userID].enabled ? "Enabled" : "Disabled", this, player.UserIDString));
    }

    #endregion

    /**
     * Save the data.
     */
    private void SaveData()
    {
      Interface.Oxide.DataFileSystem.WriteObject(Name, data);
    }

    /**
     * Is the given string a valid code?
     */
    private bool ValidCodeString(string codeString)
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
    private static string GetRandomCode()
    {
      return Core.Random.Range(0, 10000).ToString();
    }

    private void OpenCodeLockUI(BasePlayer player)
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
    private void DestoryTempCodeLock(BasePlayer player)
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

    /**
     * Notify the player that they entered a syntax error in their "use" chat command.
     */
    private void SyntaxErrorChatCommand(BasePlayer player, string Label, string[] Args)
    {
      player.ChatMessage(
        string.Format(
          lang.GetMessage("SyntaxError", this, player.UserIDString),
          string.Format("/{0} {1}", Label, HelpGetAllUseCommandArguments())
        )
      );
    }

    /**
     * Get all the arguments that can be supplied to the "use" command.
     */
    private string HelpGetAllUseCommandArguments()
    {
      return string.Format("<{0}>", string.Join("|", new string[] { "1234", commandCodeRandom, commandPickCode, commandToggleEnabled }));
    }

    /**
     * Load config values.
     */
    private void LoadConfigValues()
    {
      // Plugin options.
      displayPermissionErrors = GetConfigValue(new string[] { "Options", "displayPermissionErrors" }, displayPermissionErrors);

      // Plugin commands.
      commandUse = GetConfigValue(new string[] { "Commands", "Use" }, commandUse);

      if (configChanged)
      {
        SaveConfig();
      }
    }

    /**
     * Get the config value for the given settings.
     */
    private T GetConfigValue<T>(string[] settingPath, T defaultValue, bool deprecated = false)
    {
      object value = Config.Get(settingPath);
      if (value == null)
      {
        if (!deprecated)
        {
          SetConfigValue(settingPath, defaultValue);
          configChanged = true;
        }
        return defaultValue;
      }

      return Config.ConvertValue<T>(value);
    }

    /**
     * Set the config value for the given settings.
     */
    private void SetConfigValue<T>(string[] settingPath, T newValue, bool saveImmediately = false)
    {
      List<object> pathAndTrailingValue = new List<object>();
      foreach (var segment in settingPath)
      {
        pathAndTrailingValue.Add(segment);
      }
      pathAndTrailingValue.Add(newValue);

      Config.Set(pathAndTrailingValue.ToArray());

      if (saveImmediately)
      {
        SaveConfig();
      }
    }

    /**
     * The data this plugin needs to save.
     */
    private class Data
    {
      public Dictionary<ulong, PlayerSettings> playerCodes = new Dictionary<ulong, PlayerSettings>();
    }

    /**
     * The settings saved for each player.
     */
    private class PlayerSettings
    {
      public string code = null;
      public bool enabled = true;
    }
  }
}

