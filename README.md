# AutoCode

Automatically sets the code on code locks when placed.
Allow players to automatically try and unlock locked code locks with their code.

[![BSD 3 Clause license](https://img.shields.io/github/license/RebeccaStevens/eslint-config-rebeccastevens.svg?style=flat-square)](https://opensource.org/licenses/BSD-3-Clause)
[![Commitizen friendly](https://img.shields.io/badge/commitizen-friendly-brightgreen.svg?style=flat-square)](https://commitizen.github.io/cz-cli/)
[![semantic-release](https://img.shields.io/badge/%F0%9F%93%A6%F0%9F%9A%80-semantic--release-e10079.svg?style=flat-square)](https://github.com/semantic-release/semantic-release)

## Permissions

- `autocode.use` - Allows use of this plugin.
- `autocode.try` - Allows auto-trying of user auto-code on locked code locks.
- `autocode.admin` - Allows use of admin commands.

## Chat Commands

### Core Commands

_You_ refers to the player running the command.

- `code 1234` - Sets your auto-code to 1234.
- `code random` - Sets your auto-code to a random code.
- `code remove` - Removes your set auto-code (and guest auto-code).

The core commands are also available in a guest code version. e.g.

- `code guest 5678` - Sets your guest code to 5678.

### Other Commands

- `code quiet` - Toggle quiet mode. In this mode less messages will be displayed and your auto-code will be hidden.
- `code help` - Shows a detailed help message.

## Console Commands

Requires `autocode.admin` to use.

- `autocode.resetlockout *` - Removes all lock outs for all players.
- `autocode.resetlockout playerSteamId_or_playerDisplayName` - Removes lock out for the specified player.

## Configuration

Default configuration:

```json
{
  "Commands": {
    "Use": "code"
  },
  "Options": {
    "Display Permission Errors": true,
    "Spam Prevention": {
      "Attempts": 5,
      "Enable": true,
      "Exponential Lock Out Time": true,
      "Lock Out Reset Factor": 5.0,
      "Lock Out Time": 5.0,
      "Window Time": 30.0
    }
  }
}
```

### Explained

- `Commands` - Change the commands this plugin uses.
  - `Use` - Used to set the player's code.
- `Options`
  - `Display Permission Errors` - If set to false, players won't be notified if they don't have the right permissions to use this plugin.
  - `Spam Prevention` - Prevent players from changing their code too often to prevent abuse.
    - `Enable` - Whether spam protection is enabled or not.
    - `Attempts` - The number of code changes the player can make with in `Window Time` before being marked as spamming.
    - `Window Time` - The time frame (in seconds) to count the number of code changes the player has made.
    - `Lock Out Time` - How long (in seconds) a player will be locked out for. This number should be low if using exponential lock out times.
    - `Exponential Lock Out Time` - If true, each time the player is locked out, they will be locked out for double the amount of time they were previously locked out for.
    - `Lock Out Reset Factor` - Determines how long (as a multiples of lock out time) before the player is forgive for all previous lockout offenses (should be greater than 1 - has no effect if not using exponential lock out time).

## API

```cs
string GetCode(BasePlayer player, bool guest = false);
void SetCode(BasePlayer player, string code, bool guest = false);
void RemoveCode(BasePlayer player, bool guest = false);
bool IsValidCode(string codeString);
string GenerateRandomCode();
void OpenCodeLockUI(BasePlayer player, bool guest = false);
void ResetAllLockOuts();
void ResetLockOut(BasePlayer player);
```

## Development

### Bug Report or Feature Request

Open an issue on [GitHub](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/issues/new/choose).

### Want to contribute

Fork and clone the [GitHub repository](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode). Send me a PR :)