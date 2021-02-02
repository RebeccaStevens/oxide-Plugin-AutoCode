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

## Configuration

Default configuration:

```json
{
  "Commands": {
    "Use": "code"
  },
  "Options": {
    "displayPermissionErrors": true
  }
}
```

### Explained

- `Commands` - Change the commands this plugin uses.
  - `Use` - Used to set the player's code.
- `displayPermissionErrors` - If set to false, players won't be notified if
  they don't have the right permissions to use this plugin.

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
