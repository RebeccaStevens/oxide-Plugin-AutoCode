# Changelog

All notable changes to this project will be documented in this file. Dates are displayed in UTC.

# [1.5.0](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.4.1...v1.5.0) (2021-06-01)

### Features

- **noescape:** integrate with noescape plugin ([680bf36](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/680bf36cacc9fc88fde6f6f4eef9b882ac3f9274))

## [1.4.1](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.4.0...v1.4.1) (2021-04-20)

### Performance Improvements

- don't remove temp code locks before saving; instead just mark them as not saveable ([bcf5027](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/bcf50277dccbb0a875ce6770025d437fa2ec0164))
- only trigger code setting when a new code lock is deployed by a player, not just when spawned ([bf8e8ac](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/bf8e8ac51fe1b4ee02f6c2746b7d19a15e5545bb))

# [1.4.0](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.3.0...v1.4.0) (2021-02-22)

### Bug Fixes

- display guest code as well as code when code lock is placed with one ([18b587c](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/18b587ce5f6d63294ed13a822bf8056500442efa))
- formatting on message when a code lock is placed ([659e0bf](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/659e0bf98058b4f4c19c7be3f5dd5902f5527981))
- update user messages ([1a064f2](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/1a064f2a9dd261941e8048e015377e064f083ed3))

### Features

- add extended help message ([3ca82a0](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/3ca82a059591694540a5771d9f190d88aa5c1423))
- add quiet mode ([38b87b6](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/38b87b6d9d748b78b83f983bddf404fb6809e802))
- allow changing the icon displayed to the user in the in-game chat ([8874903](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/88749039acf28c499b5704b3abef60c1d3ef0640))
- allow for optional "set" before code in command ([281f71d](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/281f71dc2b0f328fe4abf9b47df8e27a8d4da790))

# [1.3.0](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.2.1...v1.3.0) (2021-02-13)

### Bug Fixes

- fix not being able to use "/code [guest] pick" ([1666c00](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/1666c001ec5189b5ee4a1f8f2f063d3a21fbb114))
- fix not being able to use "/code random" ([b98f0e0](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/b98f0e00a310b7c0075e75575d06e58f38a984b2))
- remove left in debugging code ([63d4b91](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/63d4b91ad8471cc17cab7b336aa0ed65e07cbb93))
- remove saving on serve shutdown ([c13d561](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/c13d5612fd72ea5145c7794413855bfe45c088e5))
- remove temp code locks when plugin is unloaded ([4cc345a](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/4cc345a4fe35447c3a9ff0e4db67b8abc63c3eef))

### Features

- a permission is now required to use admin commands ([ced03b4](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/ced03b4582f6e1153da7370480314720b9f044cb))

## [1.2.1](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.2.0...v1.2.1) (2021-02-10)

### Bug Fixes

- adjust privacy of internal methods ([4ef480e](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/4ef480e58d14a996f7d29249055c7fbec45b8e6b))
- getCode api method now has support for fetching the guest code ([88b8326](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/88b8326c7fa2ee41cd337e332939f7f64e292f39))

# [1.2.0](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.1.1...v1.2.0) (2021-02-10)

### Bug Fixes

- reword InvalidArgsTooMany message ([358e6cb](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/358e6cb1ef44d22b17e756324c6e5d3553ce5883))
- you're => your ([6538d50](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/6538d50ad038c7c608ab0b5ec41a855ef465efe9))

### Features

- add support for guest codes ([c76310d](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/c76310d012eaf716be528b29aa11cdaf60e893df))
- don't show placed code message when in streamer mode ([a993932](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/a993932557353a4ad9c7bc81b587a7a6ad992e54))
- replace ability to toggle plugin with ability to remove set code ([30b1022](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/30b10222d81e435aa3cddc719a937e3d28518304))
- show current code info when using /code without any args ([4d70a59](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/4d70a5975b245479ac10c88fbc1b671a10e08385))

## [1.1.1](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.1.0...v1.1.1) (2021-02-02)

### Bug Fixes

- trying to access data that doesn't exist ([d2e0c53](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/d2e0c53753abb1c994ee4c60c0f49a65ae49ca77))

# [1.1.0](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.0.3...v1.1.0) (2021-02-02)

### Features

- admin console command to remove spam lock outs ([10dc2a4](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/10dc2a42424988ce30576b8f9d31a690f01008b0))
- allow players to automatically try and unlock locked code locks with their code ([6ac8747](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/6ac8747632ba056e3699ceeb52740b8462c15795))
- spam prevention ([3d44668](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/3d446686dda601d5993669b9372f0c30c53eb166))

## [1.0.3](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.0.2...v1.0.3) (2021-01-31)

### Bug Fixes

- don't save temp code locks to the map's save file ([de7aed1](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/de7aed14fca7dd0e0f7f1deb0dd41e9ae8844b45))

## [1.0.2](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.0.1...v1.0.2) (2021-01-31)

### Bug Fixes

- fix random codes starting with 0 ([632df49](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/632df4976604e1a1c0e57b9135f5ac0f40c930f2))

### Performance Improvements

- use type overloading ([0d52735](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/0d52735eb4b9fa324543dafd21bf5dd2ad0883c4))

## [1.0.1](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/compare/v1.0.0...v1.0.1) (2021-01-27)

### Bug Fixes

- remove unused imports ([cbf4272](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/cbf4272dd6d1af0eedfcdc441696175fa09b8344))
- typo: use => user ([5229783](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/522978308d1b37a41ea48ab47f30887b345669ea))
- use "Interface.Oxide.Log\*" for logging ([2e11873](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/2e11873a543e58d5cf6b93574feb9bc4d6726029))

# 1.0.0 (2021-01-26)

### Features

- implement base functionality of the plugin ([4fc2086](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/4fc2086b8f6c58bd379c55f7d54a0977de0dbbbd))
- init project ([637166d](https://github.com/RebeccaStevens/oxide-Plugin-AutoCode/commit/637166ddab3e6c42a6d279e8e380a7c738fde8eb))
