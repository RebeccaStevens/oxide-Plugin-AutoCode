# Auto Code

[![BSD 3 Clause license](https://img.shields.io/github/license/RebeccaStevens/eslint-config-rebeccastevens.svg?style=flat-square)](https://opensource.org/licenses/BSD-3-Clause)
[![Commitizen friendly](https://img.shields.io/badge/commitizen-friendly-brightgreen.svg?style=flat-square)](https://commitizen.github.io/cz-cli/)
[![semantic-release](https://img.shields.io/badge/%F0%9F%93%A6%F0%9F%9A%80-semantic--release-e10079.svg?style=flat-square)](https://github.com/semantic-release/semantic-release)

## Features

- Automatically sets the code (and optionally guest code) on code locks placed by players.  
- Allow players to automatically try and unlock locked code locks with their code.
- Spam protect to stop players from potentially exploiting this plugin.
- Localization supprot.

## Permissions

### Player Permissions

- `autocode.player.use` - Allows use of this plugin.
- `autocode.player.try` - Allows auto-trying of player's auto-code on locked code locks.

Recommend command to grant player permissions `umod.grant group default autocode.player.*`.

### Admin Permissions

- `autocode.admin.removelockouts` - Allows removing lock outs applied to players.

Recommend command to grant admin permissions `umod.grant group admin autocode.admin.*`.

## Commands

### Player Commands

These commands require the permission `autocode.player.use` to use. They can be run in game.

#### Core Commands

*You* refers to the player running the command.

- `code 1234` - Sets your auto-code to the given code (in this case 1234).
- `code pick` - Opens the code lock interface for you to enter your auto-code.
- `code random` - Sets your auto-code to a randomly generated code.
- `code remove` - Removes your set auto-code (and guest auto-code).

The core commands are also avalibale in a guest code version. e.g.

- `code guest 5678` - Sets your guest auto-code to the given code (in this case 5678).

#### Other Commands

- `code quiet` - Toggle quiet mode. In this mode less messages will be displayed and your auto-code will be hidden.
- `code help` - Shows a detailed help message.

### Admin Commands

#### Remove Lock Outs

This command require the permission `autocode.admin.removelockouts` to use. It can be run in game as well as on the server console.

- `autocode.removelockout *` - Removes lock outs for all players.
- `autocode.removelockout playerSteamId_or_playerDisplayName` - Removes lock out for the specified player - multiple players can be specified.

## API

```cs
string? GetCode(IPlayer player, bool guest = false);
void SetCode(IPlayer player, string code, bool guest = false, bool quiet = false, bool hideCode = false);
void RemoveCode(IPlayer player, bool guest = false, bool quiet = false);
bool IsValidCode(string? code);
string GenerateRandomCode();
void OpenCodeLockUI(IPlayer player, bool guest = false);
void ToggleQuietMode(IPlayer player, bool quiet = false);
void RemoveLockOut(IPlayer player);
void RemoveAllLockOuts();
```

## Development

### Bug Report or Feature Request

Open an issue on [GitHub](https://github.com/RebeccaStevens/uMod-Rust-Plugin-AutoCode/issues/new/choose).

### Want to contribute

Fork and clone the [GitHub repository](https://github.com/RebeccaStevens/uMod-Rust-Plugin-AutoCode). Send me a PR :)
