<!--MDX_STRIP-->
<!--Do not remove the MDX_STRIP comments - they are used for our Documentation publishing process-->
# Miris Unity Integration

Welcome to the Miris Unity Integration for spatial streaming.

## Requirements

<!--END_MDX_STRIP-->
Your Unity project must be using Unity 6000.0.58f2 or newer.

For desktop hosts, the system requirements are below:

| OS      | Requirements                                                 |
| ------- | ------------------------------------------------------------ |
| Windows | Windows 11                                                   |
| Linux   | Ubuntu 22.04 LTS<br/>Support for other flavors of Linux, and other versions of Ubuntu, are not guaranteed |
| macOS   | macOS 15.0                                                   |

For deployments targeting specific devices, the minimum system requirements are below:

| Device     | Requirements                                           |
| ---------- | ------------------------------------------------------ |
| Android    | API level 32<br />Only arm64-v8a devices are supported |
| iOS/iPadOS | iOS/iPadOS 18.2                                        |
| Meta Quest | Only the Meta Quest 3 and Meta Quest 3S are supported  |

## Installation

1. Make sure [git](https://git-scm.com/) is installed on your device.

2. Open a Unity Project and use the Package Manager to install the Miris SDK. **Please ensure that you are not in Play mode before proceeding with the following steps.**

    * Navigate to Window -> Package Manager
    ![](img/package_manager_1.png)
    * Use the "+" button to `Install git package from url...`
    ![](img/package_manager_2.png)
    * Paste the following and hit install: `https://github.com/Miris-Inc/MirisSDK.git?path=Unity#latest`
    ![](img/package_manager_3.png)

3. The Miris SDK will need to install the native libaries it uses into the `Assets/Plugins/Miris` folder. If this folder does not contain platform folders with libraries inside, you will need to download them via the in editor tool. 

    * If the `Assets/Plugins/Miris` folder already contains the libraries, you can skip to step 6.

<!--MDX_ACCORDION="Downloading the Native Libraries"-->
4. Using a GitHub PAT for library downloading
    * If you already have a token, skip to step 5. 

    * We strongly recommend using a Fine-Grained token for this process. Visit https://github.com/settings/personal-access-tokens - and click "Generate New Token"
    ![](img/git/generate-new-token.png)

    * Set a reasonable token name and token expiration date. Set "Miris-Inc" as the Resource Owner. Select "Only select repositories", and then select "Miris-Inc/MirisSDK" in the dropdown.
    ![](img/git/set-token-params-1.png)

    * Add permissions to this token. Select "Repositories", click "Add Permissions", and then select "Contents" from the dropdown.
    ![](img/git/set-token-params-2.png)

    * Click "Generate Token". **Save this token in a secrets or password manager.**
<!--END_MDX_ACCORDION-->

5. Miris Platform Downloader Editor Tool
    * In the Unity Editor window, in the toolbar, select Tools -> Miris -> Platform Downloader
      ![](img/git/downloader-1.png)

    * If you've been directed to download a specific release, change the Tag field. Otherwise, paste your token into the GitHub Token field, and then click Load Release by Tag.

    * All compatible releases should be selected for you. Click Install Selected.
      ![](img/git/downloader-2.png)
s
6. Settings changes

    * If your project is on URP (Universal Render Pipeline), then you will need to add the Gaussian Splat Render Pass component.  If not, skip to step 7.

    * In your Assets folder, find the Settings folder. In there you will need to update the Renderer assets to add a component/render feature: `Gaussian Splat Render Pass`

    Our gaussian splat renderer requires certain shader intrinsics that may or may not be available with your project's Graphics API, especially on Windows and Linux. For the smoothest rendering experience, we recommend using the Vulkan API.

    * Go to `Edit` -> `Project Settings` -> `Player` -> `Other Settings` -> `Rendering`.
    * If `Auto Graphics API` for your platform is enabled, disable it.
    * Look for the `Graphics API` item. Ensure that `Vulkan` is the only entry present by using the `-` button to remove entries and the `+` button to add the `Vulkan` entry, if not already present.

7. Prefab setup

    * Drop the Miris Stream and `Miris Stream Controller` prefab into your scene.
    ![Prefab Setup](img/prefab_setup.png)
    * On the `Miris Stream` prefab, enter the URL for the asset you want to stream. For example `https://devcontents3.miris.com/prod/tokyo/1x1/structure.usda`

8. Stream!

    * There are editor scripts in place to allow you to see the streaming content in the editor window without being in play mode. 
    * You can also press play to start streaming.

<!--MDX_STRIP-->
### Notes

* Miris employees should consult the centralized documentation, rather than this document.
<!--END_MDX_STRIP-->
