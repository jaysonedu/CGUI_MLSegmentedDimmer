Columbia Computer Graphics and User Interfaces Lab Spring 2024: Demonstration of Dynamically Adjustable Segmented Dimmer for Magic Leap 2 with Camera Capture

Author: Jason Qin

Date: 5/11/2024

Installation:

Windows, ML API Level: 33, OS Version: 1.6.0, C SDK Version: 1.6.0, Unity SDK Version: 2.1.0, Unreal SDK Version: 1.3.0

1. Follow the instructions on https://developer-docs.magicleap.cloud/docs/guides/unity/getting-started/install-the-tools/ to set up a Unity Magic Leap project using MLSDK. (Be sure to select MLSDK, NOT OpenXR, as the library).
2. Clone the project repository: jaysonedu/CGUI_MLSegmentedDimmer (github.com)
3. Open the main scene
4. Connect the Magic Leap 2 headset to the Unity project and allow all file transfer. 
5. If Developer Mode is not enabled on the headset, go to Settings > About > Click on Build Number 7 times
6. On the Magic Leap 2 headset, enable the Segmented Dimmer feature by going to Settings > Display > Segmented Dimming
  - Make sure that the Magic Leap 2 camera is not currently in use
7. Build the project to the Magic Leap 2
8. Upon initialization of the program, if prompted, allow Magic Leap Camera and video streaming


Overview:

The main scene of this project displays two cubes, one cyan and one red. The Segmented Dimmer mesh is applied to the cyan cube, and the red cube is a normal game object. The Segmented Dimmer material opacity will adjust based on the MLCamera stream value calculations. 
Both cyan and red cubes are grabbable, and the Segmented Dimmer mesh will follow the cyan cube. Above the cyan cube is text displaying the current Segmented Dimmer status, and a debug log with Average Luminance calculations is displayed above the handheld controller. 
	The cyan cube is the one with a Segmented Dimmer mesh, which will adjust its opacity based on the brightness values (based on Average Luminance calculations as shown) of the real world environment. In the physical lens of the headset, one can notice the dimmed portions of the dimmer panel following the virtual world location of the cyan cube.
As long as the Main Camera is an active game object, the MLCamera will continue streaming and the Segmented Dimmer feature will remain active. 
