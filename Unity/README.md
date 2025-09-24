
### Prerequisites to installing this package

1. Make sure [git](https://git-scm.com/) and [git lfs](https://git-lfs.com/) are installed on your device. 


2. Temporarily, since we are using a private repo for this package, you will need to download and set up the [Git Credential Manager](https://docs.unity3d.com/6000.2/Documentation/Manual/upm-config-https-git.html). 
	* Once installed, go to the unity project repo you want to have this package in and run `git config --global credential.helper manager`
	* Followed by `git ls-remote --heads https://github.com/Miris-Inc/MirisSDK HEAD`


3. Open a Unity Project and use the Package Manager to install the Miris SDK. 

	Window -> Package Manager

	Use the "+" icon to `Install git package from url...`

	Paste the following and hit install

	`https://github.com/Miris-Inc/MirisSDK.git?path=Unity#v0.1.3`


4. Settings changes
If your project is on URP, you will need to add the Gaussian Splat Render Pass. 
* In your Assets folder, find the Settings folder. In there you will need to update the Renderer assets to add a component/render feature: `Guassian Splat Render Pass`


5. Prefab setup 
* Drop the Miris Stream and `Miris Stream Controller` prefab into your scene. 
* Drag the controller prefab into the slot in the Stream prefab. 
* On the `Miris Stream` prefab, enter the url for an asset you want to stream. For example `https://devcontents3.miris.com/prod/tokyo/1x1/structure_drop.json`


6. Press play


### Notes: 
* **Currently investigating solutions to issues running aqua-dlls/libs built on another machine. The first versions of this SDK might not run properly on your machine due to this and potential OS security blockings.**
* This sdk has not been fully tested and is being posted mostly for format purposes in this version. 

