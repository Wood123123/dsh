> For the complete documentation index, see [llms.txt](https://wiki.project-fika.com/llms.txt). Markdown versions of documentation pages are available by appending `.md` to page URLs; this page is available as [Markdown](https://wiki.project-fika.com/advanced-features/headless-client/remote-headless-client.md).

# Remote Headless Client

Instructions for setting up a headless client to host your raids from a separate computer for maximum performance gains. This is the officially supported headless client configuration.

{% hint style="info" %}
Please make sure you have read everything on the general [Headless Client page](/advanced-features/headless-client.md) first.
{% endhint %}

## Recommended Hardware Requirements

* A dedicated machine for running the headless client.
  * Most virtual private servers (VPS) will **not** work because they do not provide the performance needed for the headless client.
* A modern CPU with at least 4 cores @ 4GHz+.
* 32 GB RAM (16 GB RAM *may* work but will provide reduced performance due to virtual paging).
* Initially around 120 GB available disk space on an SSD/NVME drive. **HDD is NOT supported.**
  * After the installation is complete and everything is tested working you will be able to delete a portion of the retail game install data to save some space.

## Notes

* The installation process below will guide you through the steps to set up the SPT server and headless client on the same machine. It is possible to separate the SPT server and the headless client, but there is no performance benefit, and the steps are more involved. Therefore, this setup will not be covered in this guide.
* Linux is NOT supported and covered by this article. Visit the dedicated Linux channel in our [Discord](https://discord.gg/project-fika) for assistance.

***

## Installation

{% stepper %}
{% step %}

### Install Escape From Tarkov using BSG Launcher

Escape From Tarkov must be installed on the machine/server where you are planning to run the headless client. This is required to ensure that you own the game.&#x20;

**Fika will NOT work if you skip this step!**
{% endstep %}

{% step %}

### Install [SPT](https://sp-mod.com/installer)

If your SPT server is currently located on a different machine, please copy it to the computer where you are planning to install the headless client. It must contain the full game. If you are unable to copy it due to the size, install SPT on the remote machine using the [SPT Installer](https://sp-mod.com/installer) instead.

**Do NOT install SPT in your official Escape From Tarkov folder!**
{% endstep %}

{% step %}

### Download [Fika-Installer](https://github.com/project-fika/Fika-Installer/releases/latest)

{% endstep %}

{% step %}

### Copy `Fika-Installer.exe` to the root of your SPT install folder

Do not copy inside `SPT` folder!

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2F5yu7c0P4PT4gSQwcgOw5%2Fimage.png?alt=media&amp;token=2cfcb1fd-3830-4df9-aeba-8681867e91df" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Run `Fika-Installer.exe`

If you get an admin rights prompt, this is normal. `Fika-Installer` requires admin rights to set up the firewall rules.
{% endstep %}

{% step %}

### Choose `Install Fika`

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FACcBaVOadt3rUPt48rNp%2Fimage.png?alt=media&amp;token=5e5c3e31-cfdf-4e98-a154-83d67341f0d6" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Confirm installation completion

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FD9VHauheMEVLMpsMRod5%2Fimage.png?alt=media&amp;token=ddf074d7-a593-40ea-9ead-e2a3cb308a5b" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### OPTIONAL: Set URL in [fika.jsonc](/fika-configuration/server.md)

{% hint style="info" %}

#### Most users *do not have to do this*.

This step is not necessary if your SPT.Server is being hosted at `https://0.0.0.0:6969` and this headless client will be on the same PC as the backend server. The below instructions are **OPTIONAL** for users with a non-standard setup.

If you are unsure, skip this step.
{% endhint %}

If your headless client is not able to connect to the backend server at `127.0.0.1:6969`, you may need to:

1. Navigate to `[SPT root folder]\SPT\user\mods\fika-server\assets\configs\`
   1. If `fika.jsonc` does not exist, run `SPT.Server.exe` once and then close it.
2. Open `fika.jsonc` in a text editor.
3. Scroll down to the `scripts -> forceIp` setting and change it to an URL where the headless client will be able to connect to the backend server.
4. Save the file and close the text editor.

<div align="left"><figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FjhjzwkuoUzZs4O3zz9NO%2Fimage.png?alt=media&amp;token=d499542a-16f1-4740-a1c4-04fe06095231" alt=""><figcaption></figcaption></figure></div>
{% endstep %}

{% step %}

### Choose `Advanced options`

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FCiHYMk9oDGbrdYMAjF8B%2Fimage.png?alt=media&amp;token=cfbf11e7-f115-41da-8c83-d0dbfd26d92d" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Choose `Install Fika Headless`

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FgKHOZgGde3c1QfVjG1zK%2Fimage.png?alt=media&amp;token=e6563c9b-36c8-41ea-8a5b-9923743bce05" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Choose `Create a new headless profile`

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FwnZ3uQjHh4eBxqaGu6Y6%2Fimage.png?alt=media&amp;token=a0bf4138-a365-4c53-bbb1-7a81e480b058" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Close `Fika-Installer` when installation is completed

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FYeEWVYyouIsBl4bwyGpQ%2Fimage.png?alt=media&amp;token=0ab6f1d1-5b99-4503-8034-3d8836ddde90" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Start `SPT.Server.exe`

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FLRc3xTCQ6XWf6cP3JDMG%2Fimage.png?alt=media&amp;token=10fb20a5-8ac6-4c58-8d21-16b5bae1cba3" alt=""><figcaption></figcaption></figure>

{% endstep %}

{% step %}

### Wait for `Server has started, happy playing` message in console

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FnPRIBPpI5GnXFawIJvFs%2Fimage.png?alt=media&amp;token=13eaeb7c-6b71-448b-b22a-e93b4882d083" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Start the headless client using `Fika Headless Manager`

The `Fika Headless Manager` will start your headless client.

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FS87Ckkpqb9TwGMl7kcNV%2Fimage.png?alt=media&amp;token=60411626-21c2-4bfa-b8f7-985aee0c532b" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Wait for the headless client to load

Two console windows will appear after running the script: the `Fika Headless Manager` and the headless client console. **Do not close them**. Wait for the headless client to load. Activity will stop in the console when loading is completed.

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FQvqzHxxj9u0pK41ruLoh%2Fimage.png?alt=media&amp;token=b089198b-8ba3-463b-8e48-4c899c732196" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Start `SPT.Launcher` and your game client

The headless client is now ready to host a raid. Start Fika (`SPT.Launcher`) on your computer.&#x20;

**Do NOT start SPT.Launcher.exe on the headless client server!**

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2F5JyGZToLjW66sqPZgBa8%2Fimage.png?alt=media&amp;token=f1b62089-ee12-4f89-8a6c-50cbb24c53d3" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Host a raid using the headless client

Navigate the in-game menu like you would normally do to reach the `Raids` menu and click `Host Raid`.

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2F99rwTHlGNcSOySnT0nqj%2Fimage.png?alt=media&amp;token=9898f962-cb01-4114-b110-f867a2d1da8b" alt=""><figcaption></figcaption></figure>

Check the `Use Headless Host` box and press `Start`. This will request the headless client to start a raid in the selected location. Wait for the headless client to load the raid.

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FPT2bor9IsAHmpiumOSn7%2Fimage.png?alt=media&amp;token=39e62c60-36f5-4e0f-9d5d-0a08a41c988b" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Start the raid when the headless client is ready

Your friend(s) can join the headless client's raid when you see this screen. When everybody has joined, press `Start Raid`.

<figure><img src="https://625400337-files.gitbook.io/~/files/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FKIBpsnthxy8OSpsWzsDI%2Fuploads%2FkJSBYyECO2hRp0vcj2ty%2Fimage.png?alt=media&amp;token=1c6fcfe8-091b-4019-839c-648f7476c097" alt=""><figcaption></figcaption></figure>
{% endstep %}

{% step %}

### Done!

{% endstep %}
{% endstepper %}

***

## Modding

The headless client is very sensitive to modding. Do **NOT** install all of the mods you use with your normal gaming client. Because the headless client is heavily modified, many mods may cause crashes or other unwanted behavior.

**✅ YOU CAN INSTALL:**

* AI mods (SAIN)
* Spawn mods (ABPS, MOAR, DONUTS)
* Mods that specifically mention headless client support (That's Lit, UIFixes, etc.)
* Any mod that reasonably changes in-raid behavior, like entering custom quest zones or having to do with inventory (VCQL, Pack'n'Strap, etc.)

**❌ DO NOT INSTALL:**

* RAM/VRAM cleaner mods (RAM Cleaner, VRAM Cleaner, etc.)
* Performance mods (DeClutter, etc.)
* Any other mods not listed in the **YOU CAN INSTALL** section

***

## Troubleshooting

* If you have mods installed and you are running into issues you should first create fresh installs all around and get the headless client working and a test raid completed without any mods other than Fika. Once you have confirmed the headless client works unmodified, you may add mods 5-10 at a time and run test raids between batches of mods.
* If you are crashing or getting errors when joining a headless hosted raid, make sure that Fika is up-to-date for everyone. Verify that you have the latest version of [Fika-Installer](https://github.com/project-fika/Fika-Installer/releases/latest) and use it both in your normal SPT+Fika folder as well as the headless folder to `Update Fika` and `Advanced` -> `Update Fika Headless`.
* If the `Use Headless Host` checkbox is greyed out, it means that the headless client is unavailable. It could be caused by unsupported mods installed in the headless client, networking issues, or other factors. Note that the headless client can only host one raid at a time. Check `BepInEx/LogOutput.log` for errors, especially warnings about incompatible mods.
* If your headless client is failing to load, launch it in Graphics mode by pressing `G` during the startup of `Fika Headless Manager` if possible. If launching in Graphics mode is not possible, look in the `<timestamp> errors.log` in the SPT log folder for hints. Often this is caused by having a malfunctioning or invalid plugin, and sometimes also from missing a plugin that is required by one of the mods on the server.
* See other common issues in [FAQ and Guides](/faqandguides.md) and [Headless Client FAQ and Common Issues](/faqandguides/headless-client-faq-and-common-issues.md)
