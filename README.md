# AutoCode

Automatically sets the code on code locks placed.  
Allow players to automatically try and unlock locked code locks with their code.

[![BSD 3 Clause license](https://img.shields.io/github/license/RebeccaStevens/eslint-config-rebeccastevens.svg?style=flat-square)](https://opensource.org/licenses/BSD-3-Clause)
[![Commitizen friendly](https://img.shields.io/badge/commitizen-friendly-brightgreen.svg?style=flat-square)](https://commitizen.github.io/cz-cli/)
[![semantic-release](https://img.shields.io/badge/%F0%9F%93%A6%F0%9F%9A%80-semantic--release-e10079.svg?style=flat-square)](https://github.com/semantic-release/semantic-release)

## Permissions

- `autocode.use` - Allows use of this plugin.
- `autocode.try` - Allows auto-trying of user code on locked code locks.

## Chat Commands

*You* refers to the player running the command.

- `/code 1234` - Sets your code to 1234.
- `/code pick` - Opens the code lock input for you to enter a code.
- `/code random` - Sets your code to a random code.
- `/code toggle` - Toggles this plugin on/off for you.

## Console Commands

Only admins can use these.

- `autocode.resetlockout *` - Removes all lock outs for all players.
- `autocode.resetlockout playerSteamId_or_playerDisplayName` - Removes lock out for the specified player.

## Configuration

Default configuration:

```json
{
  {
  "Commands": {
    "Use": "code"
  },
  "Options": {
    "displayPermissionErrors": true,
    "Spam Prevention": {
      "Attempts": 5,
      "Enable": true,
      "Exponential Lock Out Time": true,
      "Exponential Lockout Time": true,
      "Lock Out Reset Factor": 5.0,
      "Lock Out Time": 5.0,
      "Lockout Reset Factor": 5.0,
      "Lockout Time": 5.0,
      "Window Time": 30.0
    }
  }
}
```

### Explained

- `Commands` - Change the commands this plugin uses.
  - `Use` - Used to set the player's code.
- `Options`
  - `displayPermissionErrors` - If set to false, players won't be notified if they don't have the right permissions to use this plugin.
  - `Spam Prevention` - Prevent players from changing their code too often to prevent abuse.
    - `Enable` - Whether spam protection is enabled or not.
    - `Attempts` - The number of code changes the player can make with in `Window Time` before being marked as spamming.
    - `Window Time` - The time frame (in seconds) to count the number of code changes the player has made.
    - `Lock Out Time` - How long (in seconds) a player will be locked out for. This number should be low if using exponential lock out times.
    - `Exponential Lock Out Time` - If true, each time the player is locked out, they will be locked out for double the amount of time they were previously locked out for.
    - `Lock Out Reset Factor` - Determines how long (as a multiples of lock out time) before the player is forgive for all previous lockout offenses (should be greater than 1 - has no effect if not using exponential lock out time).

## API

```cs
string GetCode(BasePlayer player);
void SetCode(BasePlayer player, string code);
void ToggleEnabled(BasePlayer player);
```

## Development

### Bug Report or Feature Request

Open an issue on [GitHub](https://github.com/RebeccaStevens/uMod-Rust-Plugin-AutoCode/issues/new/choose).

### Want to contribute

Fork and clone the [GitHub repository](https://github.com/RebeccaStevens/uMod-Rust-Plugin-AutoCode). Send me a PR :)
