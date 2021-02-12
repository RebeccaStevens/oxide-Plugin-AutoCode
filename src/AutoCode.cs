using uMod.Common;
using uMod.Common.Command;
using uMod.Configuration.Toml;
using uMod.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace uMod.Plugins
{
  [Info("Auto Code", "BlueBeka", "0.0.0-development")]
  [Description("Automatically sets the code on code locks placed.")]
  class AutoCode : Plugin
  {
#nullable disable
    private Config config;
#nullable enable
    private readonly PlayerSettings playerSettings;
    private readonly Dictionary<BasePlayer, TempCodeLockInfo> tempCodeLocks;

    private const string HiddenCode = "****";

    public AutoCode()
    {
      playerSettings = new PlayerSettings();
      tempCodeLocks = new Dictionary<BasePlayer, TempCodeLockInfo>();
    }

    #region Hooks

    private void OnServerInitialized()
    {
      playerSettings.Load(this);
      Permissions.Register(this);
    }

    private void OnServerSave()
    {
      playerSettings.Save(this);
    }

    private void Unload()
    {
      RemoveAllTempCodeLocks();
      playerSettings.Save(this);
    }

    private void Loaded(Config config)
    {
      this.config = config;
    }

    private object? OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
    {
      // Not one of our temporary code locks?
      if (player == null || !tempCodeLocks.ContainsKey(player) || tempCodeLocks[player].CodeLock != codeLock)
      {
        UnsubscribeFromUnneedHooks();
        return null;
      }

      // Destroy the temporary code lock as soon as it's ok to do so.
      timer.In(0, () =>
      {
        DestoryTempCodeLock(player);
      });

      SetCode(player.IPlayer, code, tempCodeLocks[player].Guest);
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

      var player = BasePlayer.FindByID(codeLock.OwnerID);

      // No player or the player doesn't have permission?
      if (player == null || !permission.UserHasPermission(player.UserIDString, Permissions.PlayerUse))
      {
        return;
      }

      // No data for player.
      if (!playerSettings.data.ContainsKey(player.UserIDString))
      {
        return;
      }

      PlayerSettings.Data settings = playerSettings.data[player.UserIDString];

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
        IAutoCodeLocale locale = Locale<IAutoCodeLocale>(player.IPlayer);
        Reply(
          player.IPlayer,
          string.Format(
            codeLock.hasGuestCode
              ? locale.Messages.OnCodeLockPlacedWithCodeAndGuestCode
              : locale.Messages.OnCodeLockPlacedWithCode,
            Formatter.Value(Utils.ShouldHideCode(player, settings) ? HiddenCode : codeLock.code),
            Formatter.Value(Utils.ShouldHideCode(player, settings) ? HiddenCode : codeLock.guestCode)
          )
        );
      }
    }

    private object? CanUseLockedEntity(BasePlayer player, CodeLock codeLock)
    {
      // Is a player that has permission and lock is locked?
      if (
        player != null &&
        codeLock.hasCode &&
        codeLock.HasFlag(BaseEntity.Flags.Locked) &&
        permission.UserHasPermission(player.UserIDString, Permissions.PlayerUse) &&
        permission.UserHasPermission(player.UserIDString, Permissions.PlayerTry) &&
        playerSettings.data.ContainsKey(player.UserIDString)
      )
      {
        PlayerSettings.Data settings = playerSettings.data[player.UserIDString];

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

    #endregion

    #region API

    /// <summary>
    /// Get the auto-code for the given player.
    /// </summary>
    /// <param name="player">The player to get the auto-code for.</param>
    /// <param name="guest">If true, the guest auto-code will be returned instead of the main auto-code.</param>
    /// <returns>A string of the player's auto-code or null if the player doesn't have a auto-code.</returns>
    public string? GetCode(IPlayer player, bool guest = false)
    {
      if (playerSettings.data.ContainsKey(player.Id))
      {
        return guest
          ? playerSettings.data[player.Id].guestCode
          : playerSettings.data[player.Id].code;
      }

      return null;
    }

    /// <summary>
    /// Set the auto-code for the given player.
    /// </summary>
    /// <param name="player">The player to set the auto-code for.</param>
    /// <param name="code">The auto-code to set for the given player.</param>
    /// <param name="guest">If true, the guest auto-code will be set instead of the main auto-code.</param>
    /// <param name="quiet">If true, no output message will be shown to the player.</param>
    /// <param name="hideCode">If true, the new auto-code won't be displayed to the user. Has no effect if quite is true.</param>
    public void SetCode(IPlayer player, string code, bool guest = false, bool quiet = false, bool hideCode = false)
    {
      // Make sure a valid code is supplied.
      if (!IsValidCode(code))
      {
        throw new Exception("Invalid code supplied.");
      }

      if (!playerSettings.data.ContainsKey(player.Id))
      {
        playerSettings.data.Add(player.Id, new PlayerSettings.Data());
      }

      // Load the player's settings.
      PlayerSettings.Data settings = playerSettings.data[player.Id];

      IAutoCodeLocale? locale = quiet ? null : Locale<IAutoCodeLocale>(player);

      double currentTime = Utils.CurrentTime();

      // Perform spam protection.
      if (config.SpamPrevention.Enabled)
      {
        double timePassed = currentTime - settings.lastSet;
        bool lockedOut = currentTime < settings.lockedOutUntil;
        double lockOutFor = config.SpamPrevention.LockOutTime * Math.Pow(2, (config.SpamPrevention.UseExponentialLockOutTime ? settings.lockedOutTimes : 0));

        if (!lockedOut)
        {
          // Called again within spam window time?
          if (timePassed < config.SpamPrevention.WindowTime)
          {
            settings.timesSetInSpamWindow++;
          }
          else
          {
            settings.timesSetInSpamWindow = 1;
          }

          // Too many recent changes?
          if (settings.timesSetInSpamWindow > config.SpamPrevention.Attempts)
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
            Reply(
              player,
              string.Format(
                locale!.Commands.Use.Messages.SpamPrevention,
                TimeSpan.FromSeconds(Math.Ceiling(settings.lockedOutUntil - currentTime)).ToString(@"d\d\ h\h\ mm\m\ ss\s").TrimStart(' ', 'd', 'h', 'm', 's', '0'),
                config.SpamPrevention.LockOutTime,
                config.SpamPrevention.WindowTime
              )
            );
          }
          return;
        }

        // Haven't been locked out for a long time?
        if (currentTime > settings.lastLockedOut + config.SpamPrevention.LockOutRemoveFactor * lockOutFor)
        {
          // Remove their lock out.
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
        var basePlayer = Utils.GetBasePlayer(player);
        hideCode |= Utils.ShouldHideCode(basePlayer, settings);

        Reply(
          player,
          hideCode
            ? guest ? locale!.Commands.Use.Messages.OnGuestCodeUpdatedHidden : locale!.Commands.Use.Messages.OnCodeUpdatedHidden
            : string.Format(
                guest ? locale!.Commands.Use.Messages.OnGuestCodeUpdated : locale!.Commands.Use.Messages.OnCodeUpdated,
                hideCode ? HiddenCode : code
              )
        );
      }
    }

    /// <summary>
    /// Remove the given player's the auto-code.
    /// </summary>
    /// <param name="player">The player to remove the auto-code of.</param>
    /// <param name="guest">If true, the guest auto-code will be removed instead of the main auto-code.</param>
    /// <param name="quiet">If true, no output message will be shown to the player.</param>
    public void RemoveCode(IPlayer player, bool guest = false, bool quiet = false)
    {
      if (!playerSettings.data.ContainsKey(player.Id))
      {
        return;
      }

      // Load the player's settings.
      var settings = playerSettings.data[player.Id];

      if (!guest)
      {
        settings.code = null;
      }

      // Remove the guest auto-code both then removing the main auto-code and when just removing the guest auto-code.
      settings.guestCode = null;

      if (!quiet)
      {
        IAutoCodeLocale locale = Locale<IAutoCodeLocale>(player);
        Reply(
          player,
          guest
            ? locale.Commands.Use.Messages.OnGuestCodeRemoved
            : locale.Commands.Use.Messages.OnCodeRemoved
        );
      }
    }

    /// <summary>
    /// Is the given string a valid code?
    /// </summary>
    /// <param name="code">The code to test.</param>
    /// <returns>True if it's valid, otherwise false.</returns>
    public bool IsValidCode(string? code)
    {
      if (code == null)
      {
        return false;
      }

      if (code.Length == 4 && int.TryParse(code, out int parsedCode))
      {
        if (parsedCode >= 0 && parsedCode < 10000)
        {
          return true;
        }
      }

      return false;
    }

    /// <summary>
    /// Generate a random code.
    /// </summary>
    /// <returns></returns>
    public string GenerateRandomCode()
    {
      return UnityEngine.Random.Range(0, 10000).ToString("0000");
    }

    /// <summary>
    /// Open the code lock UI for the given player to set their auto-code.
    /// </summary>
    /// <param name="player">The player to open the lock UI for.</param>
    /// <param name="guest">If true, the guest auto-code will be set instead of the main auto-code.</param>
    public void OpenCodeLockUI(IPlayer player, bool guest = false)
    {
      var basePlayer = Utils.GetBasePlayer(player);

      // Make sure any old code lock is destroyed.
      DestoryTempCodeLock(basePlayer);

      // Create a temporary code lock.
      var entity = GameManager.server.CreateEntity(
        "assets/prefabs/locks/keypad/lock.code.prefab",
        basePlayer.eyes.position + new Vector3(0, -3, 0)
      );

      // Creation failed? Exit.
      if (entity == null || !(entity is CodeLock))
      {
        Logger.Error("Failed to create code lock.");
        return;
      }

      var codeLock = (CodeLock)entity;

      // Don't save this code lock.
      codeLock.enableSaving = false;

      // Associate the lock with the player.
      tempCodeLocks.Add(basePlayer, new TempCodeLockInfo(codeLock, guest));

      // Spawn and lock the code lock.
      codeLock.Spawn();
      codeLock.SetFlag(BaseEntity.Flags.Locked, true);

      // Open the code lock UI.
      codeLock.ClientRPCPlayer(null, basePlayer, "EnterUnlockCode");

      // Listen for code lock codes.
      Subscribe("OnCodeEntered");

      // Destroy the temporary code lock in 20s.
      timer.In(20f, () =>
      {
        if (tempCodeLocks.ContainsKey(basePlayer) && tempCodeLocks[basePlayer].CodeLock == codeLock)
        {
          DestoryTempCodeLock(basePlayer);
        }
      });
    }

    /// <summary>
    /// Remove all lock outs caused by spam protection.
    /// </summary>
    /// <param name="player">The player to toggle quiet mode for.</param>
    /// <param name="quiet">If true, no output message will be shown to the player.</param>
    public void ToggleQuietMode(IPlayer player, bool quiet = false)
    {
      // Load the player's settings.
      var settings = playerSettings.data[player.Id];

      settings.quietMode = !settings.quietMode;

      if (!quiet)
      {
        IAutoCodeLocale locale = Locale<IAutoCodeLocale>(player);

        if (settings.quietMode)
        {
          Reply(
            player,
            string.Format(
              "{0}\n" + Formatter.SmallLineGap + "{1}",
              locale.Commands.Use.Messages.QuietModeEnable,
              locale.Commands.Use.Messages.Help.QuietModeDetails
            )
          );
        }
        else
        {
          Reply(
            player,
            locale.Commands.Use.Messages.QuietModeDisable
          );
        }
      }
    }

    /// <summary>
    /// Remove all lock outs caused by spam protection.
    /// </summary>
    public void RemoveAllLockOuts()
    {
      foreach (string userID in playerSettings.data.Keys)
      {
        RemoveLockOut(userID);
      }
    }

    /// <summary>
    /// Remove the lock out caused by spam protection for the given player.
    /// </summary>
    public void RemoveLockOut(IPlayer player)
    {
      RemoveLockOut(player.Id);
    }

    /// <summary>
    /// Remove the lock out caused by spam protection for the given user id.
    /// </summary>
    public void RemoveLockOut(string userID)
    {
      if (!playerSettings.data.ContainsKey(userID))
      {
        return;
      }

      PlayerSettings.Data settings = playerSettings.data[userID];
      settings.lockedOutTimes = 0;
      settings.lockedOutUntil = 0;
    }

    #endregion API

    /// <summary>
    /// Destroy the temporary code lock for the given player.
    /// </summary>
    private void DestoryTempCodeLock(BasePlayer player)
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
      UnsubscribeFromUnneedHooks();
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
      UnsubscribeFromUnneedHooks();
    }

    /// <summary>
    /// Unsubscribe from things that there is not point currently being subscribed to.
    /// </summary>
    private void UnsubscribeFromUnneedHooks()
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
    private void Message(IPlayer player, string message)
    {
      if (string.IsNullOrEmpty(message) || !player.IsConnected)
        return;

      BasePlayer basePlayer = Utils.GetBasePlayer(player);

      basePlayer.SendConsoleCommand(
        "chat.add",
        2,
        config.Messages.ChatIconId,
        message
      );
    }

    /// <summary>
    /// Reply to the given player.
    /// </summary>
    private void Reply(IPlayer player, string message)
    {
      switch (player.LastCommand)
      {
        case CommandType.Chat:
          Message(player, message);
          break;
        case CommandType.Console:
          player.Reply(message);
          break;
      }
    }

    #region Localization

    /// <summary>
    /// Localization for this plugin.
    /// </summary>
    [Localization, Toml]
    private interface IAutoCodeLocale : ILocale
    {
      public ICommandsLocales Commands { get; }
      public IMessagesLocales Messages { get; }

      /// <summary>
      /// The local for each command.
      /// </summary>
      public interface ICommandsLocales
      {
        public CommandUse.ILocale Use { get; }
        public CommandLockOutRemove.ILocale LockOutRemove { get; }
      }

      public interface IMessagesLocales
      {
        public string OnCodeLockPlacedWithCode { get; }
        public string OnCodeLockPlacedWithCodeAndGuestCode { get; }
        public string Enabled { get; }
        public string Disabled { get; }
        public string NotSet { get; }
      }
    }

    /// <summary>
    /// The default (English) localization of this plugin.
    /// </summary>
    [Locale]
    private class LocaleEnglish : IAutoCodeLocale
    {
      public IAutoCodeLocale.ICommandsLocales Commands => new CommandsLocales();

      public IAutoCodeLocale.IMessagesLocales Messages => new MessagesLocale();

      public class MessagesLocale : IAutoCodeLocale.IMessagesLocales
      {
        public string OnCodeLockPlacedWithCode => "Code lock placed with code {0}.";
        public string OnCodeLockPlacedWithCodeAndGuestCode => "Code lock placed with code {0} and guest code {1}.";
        public string Enabled => "Enabled";
        public string Disabled => "Disabled";
        public string NotSet => "Not set";
      }

      public class CommandsLocales : IAutoCodeLocale.ICommandsLocales
      {
        public CommandUse.ILocale Use => new CommandUseLocale();

        public CommandLockOutRemove.ILocale LockOutRemove => new CommandLockOutRemoveLocale();

        public class CommandUseLocale : CommandUse.ILocale
        {
          public string Command => "code";
          public CommandUse.ILocale.IArguments Arguments => new CommandUseArgumentsLocale();
          public CommandUse.ILocale.IMessages Messages => new MessagesLocale();

          public class CommandUseArgumentsLocale : CommandUse.ILocale.IArguments
          {
            public string Guest => "guest";
            public string SetCode => "set";
            public string PickCode => "pick";
            public string RandomCode => "random";
            public string RemoveCode => "remove";
            public string QuietMode => "quiet";
            public string ShowHelp => "help";
          }

          public class MessagesLocale : CommandUse.ILocale.IMessages
          {
            public string Description => "Automatically set the code on code locks you place.";
            public string Usage => "Usage:\n{0}";
            public string Info => "Code: {0}\nGuest Code: {1}\nQuiet Mode: {2}";
            public string OnCodeUpdated => "Your auto-code has changed to {0}.";
            public string OnCodeUpdatedHidden => "New auto-code set.";
            public string OnGuestCodeUpdated => "Your guest auto-code has changed to {0}.";
            public string OnGuestCodeUpdatedHidden => "New guest auto-code set.";
            public string OnCodeRemoved => "Your auto-code has been removed.";
            public string OnGuestCodeRemoved => "Your guest auto-code has been removed.";
            public string InvalidCode => "Invalid code. Code must be between 0000 and 9999.";
            public string SyntaxError => "Syntax Error: expected command in the form:\n{0}";
            public string SpamPrevention => "Too many recent auto-code sets. Please wait {0} and try again.";
            public string QuietModeEnable => "Quite mode now enabled.";
            public string QuietModeDisable => "Quite mode now disabled.";
            public CommandUse.ILocale.IMessages.IHelpExtended Help => new HelpExtendedDef();

            public class HelpExtendedDef : CommandUse.ILocale.IMessages.IHelpExtended
            {
              public string CoreCommands => "Core Commands:";
              public string OtherCommands => "Other Commands:";
              public string Info => "Show your settings:\n{0}";
              public string SetCode => "Set your auto-code to 1234:\n{0}";
              public string PickCode => "Open the code lock interface to set your auto-code:\n{0}";
              public string RandomCode => "Set your auto-code to a randomly generated code:\n{0}";
              public string RemoveCode => "Remove your set auto-code:\n{0}";
              public string CoreGuestCommands => "Each core command is also avalibale in a guest code version. e.g.\n{0}";
              public string QuietMode => "Toggles on/off quiet mode:\n{0}";
              public string QuietModeDetails => "Less messages will be shown and your auto-code will be hidden.";
              public string Help => "Displays this help message:\n{0}";
            }
          }
        }

        public class CommandLockOutRemoveLocale : CommandLockOutRemove.ILocale
        {
          public string Command => "autocode.removelockout";
          CommandLockOutRemove.ILocale.IMessages CommandLockOutRemove.ILocale.Messages => new MessagesLocale();

          public class MessagesLocale : CommandLockOutRemove.ILocale.IMessages
          {
            string CommandLockOutRemove.ILocale.IMessages.Help => "Usage:\n{0} *\n{0} playerIdOrName [playerIdOrName] ...";
            string CommandLockOutRemove.ILocale.IMessages.Description => "Remove the spam lock out for the specified player(s).";
            string CommandLockOutRemove.ILocale.IMessages.ErrorCannotFindPlayer => "Error: Cannot find player \"{0}\".";
            string CommandLockOutRemove.ILocale.IMessages.LockOutsRemovedForAll => "Lock outs removed for all players.";
            string CommandLockOutRemove.ILocale.IMessages.LockOutRemovedFor => "Lock outs removed for {0}.";
          }
        }
      };
    }

    #endregion

    /// <summary>
    /// The Config for this plugin.
    /// </summary>
    [Config(Version = "1.0.0"), Toml]
    private new class Config
    {
      [TomlProperty(Comment = "Settings related to messages that are display to users.")]
      public MessagesDef Messages = new MessagesDef();

      public class MessagesDef
      {
        [TomlProperty(Comment = "The steam profile ID to use the display picture of when displaying messages.")]
        public ulong ChatIconId = 76561199143387303u;
      }

      [TomlProperty(Comment = "Prevent players from changing their auto-code too often to prevent abuse.")]
      public SpamPreventionDef SpamPrevention = new SpamPreventionDef();

      public class SpamPreventionDef
      {
        [TomlProperty(Comment = "Whether spam protection is enabled or not.")]
        public bool Enabled = true;

        [TomlProperty(Comment = "The number of auto-code changes the player can make with in `WindowTime` before being marked as spamming.")]
        public int Attempts = 5;

        [TomlProperty(Comment = "The time frame (in seconds) to count the number of auto-code changes the player has made.")]
        public double WindowTime = 30.0;

        [TomlProperty(Comment = "How long (in seconds) a player will be locked out for. This number should be low if using exponential lock out times.")]
        public double LockOutTime = 5.0;

        [TomlProperty(Comment = "If true, each time the player is locked out, they will be locked out for double the amount of time they were previously locked out for.")]
        public bool UseExponentialLockOutTime = true;

        [TomlProperty(Comment = "Determines how long (as a multiples of lock out time) before the player is forgive for all previous lockout offenses (should be greater than 1 - has no effect if not using exponential lock out time).")]
        public double LockOutRemoveFactor = 5.0;
      }
    }

    /// <summary>
    /// The player settings.
    /// </summary>
    [Toml(Filename)]
    private class PlayerSettings
    {
      // The filename for this data file.
      private const string Filename = "PlayerSettings";

      // The data for each player.
      public Dictionary<string, Data> data;

      public PlayerSettings()
      {
        data = new Dictionary<string, Data>();
      }

      /// <summary>
      /// The data saved for each individual player.
      /// </summary>
      public class Data
      {
        public string? code = null;
        public string? guestCode = null;
        public bool quietMode = false;
        public double lastSet = 0;
        public int timesSetInSpamWindow = 0;
        public double lockedOutUntil = 0;
        public double lastLockedOut = 0;
        public int lockedOutTimes = 0;
      }

      /// <summary>
      /// Save the data.
      /// </summary>
      public void Save(AutoCode plugin)
      {
        IDataFile<PlayerSettings> dataFile = plugin.Files.GetDataFile<PlayerSettings>(Filename);
        dataFile.Object = plugin.playerSettings;
        dataFile.SaveAsync().Fail(plugin.Logger.Report);
      }

      /// <summary>
      /// Load the data.
      /// </summary>
      public void Load(AutoCode plugin)
      {
        IDataFile<PlayerSettings> dataFile = plugin.Files.GetDataFile<PlayerSettings>(Filename);
        dataFile.LoadAsync()
          .Then((PlayerSettings playerSettings) =>
          {
            plugin.playerSettings.data = playerSettings.data;
          })
          .Fail((Exception exception) =>
          {
            // No existing settings?
            if (exception is System.IO.FileNotFoundException)
            {
              plugin.Logger.Debug("No existing player settings found.");
              return;
            }

            plugin.Logger.Report(exception);
          });
      }
    }

    /// <summary>
    /// The permissions this plugin uses.
    /// </summary>
    private static class Permissions
    {
      // Permissions.
      public const string PlayerUse = "autocode.player.use";
      public const string PlayerTry = "autocode.player.try";
      public const string AdminRemoveLockOuts = "autocode.admin.removelockouts";

      /// <summary>
      /// Register the permissions.
      /// </summary>
      public static void Register(AutoCode plugin)
      {
        if (!plugin.permission.PermissionExists(PlayerUse, plugin))
        {
          plugin.permission.RegisterPermission(PlayerUse, plugin);
        }

        if (!plugin.permission.PermissionExists(PlayerTry, plugin))
        {
          plugin.permission.RegisterPermission(PlayerTry, plugin);
        }

        if (!plugin.permission.PermissionExists(AdminRemoveLockOuts, plugin))
        {
          plugin.permission.RegisterPermission(AdminRemoveLockOuts, plugin);
        }
      }
    }

    /// <summary>
    /// The command to "use" this plugin.
    /// </summary>
    [Command("Commands.Use.Command {arg0?} {arg1?} {arg2?}")]
    [Permission(Permissions.PlayerUse)]
    [Locale(typeof(IAutoCodeLocale))]
    [Description("Commands.Use.Messages.Description")]
    public void UseCommand(IPlayer player, IArgs args)
    {
      IAutoCodeLocale locale = Locale<IAutoCodeLocale>(player);

      // Permission attribute doesn't work yet to check permissions so check for permission manually.
      if (!permission.UserHasPermission(player.Id, Permissions.PlayerUse))
      {
        if (Utility.Plugins.Configuration.Commands.DeniedCommands)
          player.Reply(Interface.uMod.GetStrings(player).Command.PermissionDenied.Interpolate("command", locale.Commands.Use.Command));
        return;
      }

      // Are args valid according to the command signature?
      if (!args.IsValid)
      {
        Logger.Debug(string.Format("Invalid arguments, got:\n{0}.", args));
        CommandUse.ReplyWithSyntaxError(this, player, locale);
        return;
      }

      // Load the arguments passed in.
      string?[] argsArray = new string?[] {
        args.GetString("arg0"),
        args.GetString("arg1"),
        args.GetString("arg2")
      };
      int nextArg = 0;

      bool guest = argsArray[nextArg]?.ToLower() == locale.Commands.Use.Arguments.Guest;
      if (guest)
        nextArg++;

      // If guest, there must be another argument
      if (guest && string.IsNullOrEmpty(argsArray[nextArg]))
      {
        CommandUse.ReplyWithSyntaxError(this, player, locale);
        return;
      }

      var task = argsArray[nextArg++]?.ToLower();
      string[]? taskArgs = null;

      if (string.IsNullOrEmpty(task))
      {
        task = "info";
      }
      else if (task == locale.Commands.Use.Arguments.SetCode)
      {
        var code = argsArray[nextArg++];
        if (IsValidCode(code))
          taskArgs = new string[] { code! };
        else
          Reply(player, locale.Commands.Use.Messages.InvalidCode);
        return;
      }
      else if (IsValidCode(task))
      {
        taskArgs = new string[] { task };
        task = locale.Commands.Use.Arguments.SetCode;
      }

      // Not all args used?
      if (nextArg < args.Length)
      {
        Logger.Debug(string.Format("Invalid arguments, got:\n{0}\nOnly used {1} args of {2}.", args, nextArg, args.Length));
        CommandUse.ReplyWithSyntaxError(this, player, locale);
        return;
      }

      CommandUse.Handle(this, player, task, taskArgs, guest, locale);
    }

    /// <summary>
    /// The "use" command.
    /// </summary>
    private static class CommandUse
    {
      /// <summary>
      /// The implementation of the use command.
      /// </summary>
      public static void Handle(AutoCode plugin, IPlayer player, string task, string[]? taskArgs, bool guest, IAutoCodeLocale locale)
      {
        // Create settings for user if they don't already have any settings.
        if (!plugin.playerSettings.data.ContainsKey(player.Id))
        {
          plugin.playerSettings.data.Add(player.Id, new PlayerSettings.Data());
        }

        // Info?
        if (task == "info")
        {
          ShowInfo(plugin, player, locale);
          return;
        }

        // Set code?
        if (task == locale.Commands.Use.Arguments.SetCode)
        {
          plugin.SetCode(player, taskArgs![0], guest);
          return;
        }

        // Pick code?
        if (task == locale.Commands.Use.Arguments.PickCode)
        {
          plugin.OpenCodeLockUI(player, guest);
          return;
        }

        // Toggle quiet mode?
        if (task == locale.Commands.Use.Arguments.QuietMode)
        {
          plugin.ToggleQuietMode(player);
          return;
        }

        // Remove?
        if (task == locale.Commands.Use.Arguments.RemoveCode)
        {
          plugin.RemoveCode(player, guest);
          return;
        }

        // Use random code?
        if (task == locale.Commands.Use.Arguments.RandomCode)
        {
          var hideCode = false;
          if (plugin.playerSettings.data.ContainsKey(player.Id))
          {
            PlayerSettings.Data settings = plugin.playerSettings.data[player.Id];
            hideCode = settings.quietMode;
          }

          plugin.SetCode(player, plugin.GenerateRandomCode(), guest, false, hideCode);
          return;
        }

        // Help?
        if (task == locale.Commands.Use.Arguments.ShowHelp)
        {
          plugin.Reply(player, GetHelpExtended(plugin, player, locale));
          return;
        }

        ReplyWithSyntaxError(plugin, player, locale);
      }

      /// <summary>
      /// Show the player their settings and the plugin info.
      /// </summary>
      public static void ShowInfo(AutoCode plugin, IPlayer player, IAutoCodeLocale locale)
      {
        string? code = null;
        string? guestCode = null;
        bool quietMode = false;

        if (plugin.playerSettings.data.ContainsKey(player.Id))
        {
          PlayerSettings.Data settings = plugin.playerSettings.data[player.Id];
          code = settings.code;
          guestCode = settings.guestCode;
          quietMode = settings.quietMode;

          BasePlayer basePlayer = Utils.GetBasePlayer(player);

          if (Utils.ShouldHideCode(basePlayer, settings))
          {
            code = code == null ? null : HiddenCode;
            guestCode = guestCode == null ? null : HiddenCode;
          }
        }

        plugin.Reply(
          player,
          string.Format(
            "{0}\n" + Formatter.SmallLineGap + "{1}\n" + Formatter.SmallLineGap + "{2}",
            Formatter.H3(locale.Commands.Use.Messages.Description),
            string.Format(
              locale.Commands.Use.Messages.Info,
              code != null ? Formatter.Value(code) : Formatter.NoValue(locale.Messages.NotSet),
              guestCode != null ? Formatter.Value(guestCode) : Formatter.NoValue(locale.Messages.NotSet),
              quietMode ? Formatter.Value(locale.Messages.Enabled) : Formatter.NoValue(locale.Messages.Disabled)
            ),
            GetUsage(locale)
          )
        );
      }

      /// <summary>
      /// Notify the player that they entered a syntax error.
      /// </summary>
      public static void ReplyWithSyntaxError(AutoCode plugin, IPlayer player, IAutoCodeLocale locale)
      {
        plugin.Reply(
          player,
          string.Format(
            locale.Commands.Use.Messages.SyntaxError,
            GetCommandSyntax(locale)
          )
        );
      }

      /// <summary>
      /// How to use this command.
      /// </summary>
      public static string GetUsage(IAutoCodeLocale locale)
      {
        return string.Format(
          locale.Commands.Use.Messages.Usage,
          GetCommandSyntax(locale)
        );
      }

      /// <summary>
      /// Shows the valid syntax for using this command.
      /// </summary>
      private static string GetCommandSyntax(IAutoCodeLocale locale)
      {
        return Formatter.Command(
          string.Format(
            "{0} [<{1}>]",
            locale.Commands.Use.Command,
            string.Join("|", new string[] {
              string.Format(
                "[{0}] <{1}>",
                locale.Commands.Use.Arguments.Guest,
                string.Join("|", new string[] {
                  string.Format(
                    "[{0}] {1}",
                    locale.Commands.Use.Arguments.SetCode,
                    "1234"
                  ),
                  locale.Commands.Use.Arguments.PickCode,
                  locale.Commands.Use.Arguments.RandomCode,
                  locale.Commands.Use.Arguments.RemoveCode
                })
              ),
              locale.Commands.Use.Arguments.QuietMode,
              locale.Commands.Use.Arguments.ShowHelp
            })
          )
        );
      }

      /// <summary>
      /// Display an extended help messsage to the player.
      /// </summary>
      public static string GetHelpExtended(AutoCode plugin, IPlayer player, IAutoCodeLocale locale)
      {
        return string.Format(
          "{0}",
          string.Join(
            "\n" + Formatter.SmallLineGap,
            string.Join(
              "\n" + Formatter.SmallLineGap,
              Formatter.H3(locale.Commands.Use.Messages.Help.CoreCommands),
              Formatter.UL(
                string.Format(
                  locale.Commands.Use.Messages.Help.Info,
                  Formatter.Indent(Formatter.Command(string.Format("{0}", locale.Commands.Use.Command)))
                ),
                string.Format(
                  locale.Commands.Use.Messages.Help.SetCode,
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", locale.Commands.Use.Command, "1234")))
                ),
                string.Format(
                  locale.Commands.Use.Messages.Help.PickCode,
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", locale.Commands.Use.Command, locale.Commands.Use.Arguments.PickCode)))
                ),
                string.Format(
                  locale.Commands.Use.Messages.Help.RandomCode,
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", locale.Commands.Use.Command, locale.Commands.Use.Arguments.RandomCode)))
                ),
                string.Format(
                  locale.Commands.Use.Messages.Help.RemoveCode,
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", locale.Commands.Use.Command, locale.Commands.Use.Arguments.RemoveCode)))
                )
              ),
              string.Format(
                locale.Commands.Use.Messages.Help.CoreGuestCommands,
                Formatter.Indent(Formatter.Command(string.Format("{0} {1} {2}", locale.Commands.Use.Command, locale.Commands.Use.Arguments.Guest, "5678")))
              )
            ),
            string.Join(
              "\n" + Formatter.SmallLineGap,
              Formatter.H3(locale.Commands.Use.Messages.Help.OtherCommands),
              Formatter.UL(
                string.Format(
                  locale.Commands.Use.Messages.Help.QuietMode,
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", locale.Commands.Use.Command, locale.Commands.Use.Arguments.QuietMode)))
                ),
                string.Format(
                  locale.Commands.Use.Messages.Help.Help,
                  Formatter.Indent(Formatter.Command(string.Format("{0} {1}", locale.Commands.Use.Command, locale.Commands.Use.Arguments.ShowHelp)))
                )
              )
            )
          )
        );
      }

      /// <summary>
      /// All the data that needs to be localized.
      /// </summary>
      public interface ILocale
      {
        public string Command { get; }
        public IArguments Arguments { get; }

        public IMessages Messages { get; }

        public interface IArguments
        {
          public string Guest { get; }
          public string PickCode { get; }
          public string QuietMode { get; }
          public string RandomCode { get; }
          public string RemoveCode { get; }
          public string SetCode { get; }
          public string ShowHelp { get; }
        }

        public interface IMessages
        {
          public string Usage { get; }
          public string Description { get; }
          public string Info { get; }
          public string OnCodeUpdated { get; }
          public string OnCodeUpdatedHidden { get; }
          public string OnGuestCodeUpdated { get; }
          public string OnGuestCodeUpdatedHidden { get; }
          public string OnCodeRemoved { get; }
          public string OnGuestCodeRemoved { get; }
          public string InvalidCode { get; }
          public string SyntaxError { get; }
          public string SpamPrevention { get; }
          public string QuietModeEnable { get; }
          public string QuietModeDisable { get; }
          public IHelpExtended Help { get; }

          public interface IHelpExtended
          {
            public string CoreCommands { get; }
            public string OtherCommands { get; }
            public string Info { get; }
            public string SetCode { get; }
            public string PickCode { get; }
            public string RandomCode { get; }
            public string RemoveCode { get; }
            public string CoreGuestCommands { get; }
            public string QuietMode { get; }
            public string QuietModeDetails { get; }
            public string Help { get; }
          }
        }
      }
    }

    /// <summary>
    /// The command to remove player lock outs.
    /// </summary>
    [Command("Commands.LockOutRemove.Command {players*}")]
    [Permission(Permissions.AdminRemoveLockOuts)]
    [Locale(typeof(IAutoCodeLocale))]
    [Description("Commands.LockOutRemove.Messages.Description")]
    public void LockOutRemoveCommand(IPlayer player, IArgs args)
    {
      IAutoCodeLocale locale = Locale<IAutoCodeLocale>(player);

      // Permission attribute doesn't work yet to check permissions so check for permission manually.
      if (!permission.UserHasPermission(player.Id, Permissions.AdminRemoveLockOuts))
      {
        if (Utility.Plugins.Configuration.Commands.DeniedCommands)
          player.Reply(Interface.uMod.GetStrings(player).Command.PermissionDenied.Interpolate("command", locale.Commands.LockOutRemove.Command));
        return;
      }

      // Are args valid according to the command signature?
      if (!args.IsValid)
      {
        Reply(
          player,
          string.Format(
            "{0}\n\n{1}",
            locale.Commands.LockOutRemove.Messages.Description,
            CommandLockOutRemove.GetHelp(locale)
          )
        );
        return;
      }

      // Load the arguments passed in.
      string[] removeFor = args.GetArgument<string[]>("players");

      CommandLockOutRemove.Handle(this, player, removeFor, locale);
    }

    private static class CommandLockOutRemove
    {
      /// <summary>
      /// Run this command.
      /// </summary>
      /// <returns></returns>
      public static void Handle(AutoCode plugin, IPlayer player, string[] removeFor, IAutoCodeLocale locale)
      {
        if (removeFor.Length == 0)
        {
          return;
        }

        // Remove all?
        if (removeFor.Length == 1 && removeFor[0] == "*")
        {
          plugin.Reply(player, locale.Commands.LockOutRemove.Messages.LockOutsRemovedForAll);
          plugin.RemoveAllLockOuts();
          return;
        }

        // Find the players to remove for.
        List<BasePlayer> removeForList = new List<BasePlayer>();
        foreach (string id in removeFor)
        {
          BasePlayer? p = BasePlayer.Find(id);
          if (p == null)
          {
            plugin.Reply(
              player,
              string.Format(
                locale.Commands.LockOutRemove.Messages.ErrorCannotFindPlayer,
                id
              )
            );
            return;
          }

          removeForList.Add(p);
        }

        // Rest for players.
        plugin.RemoveLockOut(removeForList[0].IPlayer);
        plugin.Reply(
          player,
          string.Format(
            locale.Commands.LockOutRemove.Messages.LockOutRemovedFor,
            string.Join(", ", removeForList.Select((p) => p.displayName))
          )
        );
        return;
      }

      /// <summary>
      /// Explains how to use this command.
      /// </summary>
      public static string GetHelp(IAutoCodeLocale locale)
      {
        return string.Format(
          locale.Commands.LockOutRemove.Messages.Help,
          GetUsage(locale)
        );
      }

      /// <summary>
      /// Explains how to use this command.
      /// </summary>
      private static string GetUsage(IAutoCodeLocale locale)
      {
        return locale.Commands.LockOutRemove.Command;
      }

      /// <summary>
      /// All the data that needs to be localized.
      /// </summary>
      public interface ILocale
      {
        public string Command { get; }
        public IMessages Messages { get; }

        public interface IMessages
        {
          public string Help { get; }
          public string Description { get; }
          public string ErrorCannotFindPlayer { get; }
          public string LockOutsRemovedForAll { get; }
          public string LockOutRemovedFor { get; }
        }
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
      public const string SmallerLineGap = "<size=7.5>\n</size>";

      /// <summary>
      /// A line break in a small font.
      /// </summary>
      public const string SmallLineGap = "<size=9>\n</size>";

      /// <summary>
      /// Format the given text as a header level 1.
      /// <param name="text">The text to format.</param>
      /// </summary>
      public static string H1(string text)
      {
        return string.Format("<size=20>{0}</size>", text);
      }

      /// <summary>
      /// Format the given text as a header level 2.
      /// <param name="text">The text to format.</param>
      /// </summary>
      public static string H2(string text)
      {
        return string.Format("<size=18>{0}</size>", text);
      }

      /// <summary>
      /// Format the given text as a header level 3.
      /// <param name="text">The text to format.</param>
      /// </summary>
      public static string H3(string text)
      {
        return string.Format("<size=16>{0}</size>", text);
      }

      /// <summary>
      /// Small text.
      /// <param name="text">The text to format.</param>
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
      /// <param name="text">The text to indent.</param>
      /// <param name="amount">The amount to indent the text by.</param>
      /// <param name="size">The size of each indent.</param>
      /// </summary>
      public static string Indent(string text, int amount = 1, int size = 4)
      {
        var indent = new String(' ', amount * size);
        return indent + text;
      }

      /// <summary>
      /// Format the given text as a command.
      /// <param name="text">The text to format.</param>
      /// </summary>
      public static string Command(string text)
      {
        return string.Format("<color=#e0e0e0>{0}</color>", text);
      }

      /// <summary>
      /// Format the given text as a value.
      /// <param name="text">The text to format.</param>
      /// </summary>
      public static string Value(string text)
      {
        return string.Format("<color=#bfff75>{0}</color>", text);
      }

      /// <summary>
      /// Format the given text as a non-value (e.g. a null value).
      /// <param name="text">The text to format.</param>
      /// </summary>
      public static string NoValue(string text)
      {
        return string.Format("<color=#ff7771>{0}</color>", text);
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
      /// Get the BasePlayer of the given IPlayer
      /// </summary>
      public static BasePlayer GetBasePlayer(IPlayer player)
      {
        return (BasePlayer)player.Object;
      }

      /// <summary>
      /// Should the code for the given player be hidden in messages?
      /// </summary>
      public static bool ShouldHideCode(BasePlayer player, PlayerSettings.Data settings)
      {
        return settings.quietMode || player.net.connection.info.GetBool("global.streamermode");
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
