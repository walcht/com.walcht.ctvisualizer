# Unity CTVisualizer

<!--toc:start-->
- [Unity CTVisualizer](#unity-ctvisualizer)
  - [Installation \& Build Instructions](#installation--build-instructions)
    - [Build Instructions for the Magic Leap 2 Platform](#build-instructions-for-the-magic-leap-2-platform)
  - [Usage](#usage)
  - [Project Structure](#project-structure)
  - [Render Modes](#render-modes)
    - [In Core (IC) Rendering Mode](#in-core-ic-rendering-mode)
    - [Out-of-Core (OOC) Virtual Memory (VM) Rendering Mode](#out-of-core-ooc-virtual-memory-vm-rendering-mode)
    - [Out-of-Core (OOC) Hybrid Rendering Mode](#out-of-core-ooc-hybrid-rendering-mode)
  - [Known Issues](#known-issues)
    - [Universal Rendering Pipeline Shader Issues](#universal-rendering-pipeline-shader-issues)
    - [Blank Screen When Cross-Compiling on Linux for the Magic Leap 2](#blank-screen-when-cross-compiling-on-linux-for-the-magic-leap-2)
  - [License](#license)
<!--toc:end-->

A Unity3D package for efficiently visualizing and manipulating very large (in
the range of 100GBs) CT/MRI volumetric datasets. The package comes with a set
of samples for different target platforms (e.g., desktop, Magic Leap 2).

## Installation & Build Instructions

The project is provided as a separate Unity package that can be easily added to your project by:

  1. Window -> Package Manager -> (Top left + icon) -> Install package from Git URL -> Add this repo's link:

      ```
      https://github.com/walcht/com.walcht.ctvisualizer.git
      ```
    
      After having imported CTVisualizer, you may encounter some missing dependency(ies) issues. Make sure to close and
      open the Unity editor to trigger a custom resolver for the Git package dependencies (Unity's default package
      manager does not support Git packages. Yeah, you read that right...). In case the missing dependency packages are
      not resolved, navigate to ```package.json -> git-dependencies``` and install them manually.

  1. This project makes use of a [native C++ rendering plugin][1] to augment Unity's graphics API to be able to create
   larger-than-2GBs textures and upload chunks to them. Follow the instructions in [TextureSubPlugin][1] to compile the
   plugin for your target platform (Windows, Linux, MagicLeap2, or Android).

  2. CTVisualizer expects input datasets in the form of Chunked Volumetric DataSet (CVDS). A separate, offline, Python
   CVDS converter is needed and can be installed from [here][2].

Tested on these Unity versions for these target platforms:

| Unity Version | Host Platform  | Target Platform | Status             | Notes |
| ------------- | -------------- | --------------- | ------------------ | ----- |
| 6000.0.40f1   | Windows 10     | Windows 10      | :white_check_mark: |       |
| 6000.0.40f1   | Ubuntu 22.04.5 | Magic Leap 2    | :white_check_mark: | might get a black screen - see Known Issues below|
| 6000.0.40f1   | Windows 10     | Magic Leap 2    | TODO               |       |
| 6000.0.40f1   | Ubuntu 22.04.5 | Ubuntu 22.04.5  | TODO               |       |


### Build Instructions for the Magic Leap 2 Platform

To build for the Magic Leap 2 AR device:

  1. Connect the device to a machine (preferably Windows-based<sup>1</sup>)
  2. Follow the instructions on this [repository][1] to build the native plugin for the ML2 device
  3. Import the *magicleap2* sample scene
  4. Install the Magic Leap 2 SDK package dependency from: ```https://github.com/magicleap/MagicLeapUnitySDK.git```
  5. Switch to the magicleap2 build profile (you can find the build profile asset in the Settings folder of the imported sample)
  6. Check the OpenXR project validator for potential issues and fix them
  7. Build the project (of course, don't forget to add the magicleap2 scene)
  8. After having finished the build process, navigate to the build directory and run:
   
      ```shell
      adb install ctvisualizer.x86_64.apk
      ```
      or, if you are on a Windows platform, you can simply use the Magic Leap Hub to install it through the GUI.

  9. Copy your converted [CVDS]() dataset(s) into the [Application.persistentDataPath][3] on the attached ML2 device using:
  
      ```shell
      adb push <path-to-cvds-dataset-folder> /storage/emulated/0/Android/data/com.walcht.ctvisualizer/files/
      ```

  10. You can also optionally copy other resources to the same directory above such as: serialized transfer functions,
   serialized visualization parameters, etc.

  11. Run the just-installed *ctvisualizer* app on the ML2<sup>2</sup>
  12. You can control the volumetric object using hand gestures such as grasping and pinching for rotating and scaling
  the object, respectively.

---

 <sup>1</sup>: See Known Issues for a Linux host platform.

 <sup>2</sup>: For debugging potential issues on the ML2, before starting the *ctvisualizer* app, run:

  ```shell
  adb shell logcat | grep "Unity"
  ```

  Make sure to keep an eye for errors and exceptions (especially OpenXR-related thrown exceptions)

## Usage 

UnityCT-Visualizer is a UI-centric application - all operations are mainly
done through the provided GUI. To visualize a CT/MRI dataset using CTVisualizer, you have to:

  1. Convert your dataset into CVDS format using the [CVDS Python converter package][1].
  2. Copy/Move the converted CVDS dataset into your [Application.persistentDataPath][3].
  4. Click on ```SELECT``` to select a CVDS dataset from the Application.persistentDataPath.
  5. Adjust the pipeline parameters (these are runtime-constant parameters) and optionally the debugging parameters.
  6. Click on ```VISUALIZE``` to start the visualization process of the selected CVDS dataset.
  7. A volumetric object should appear alongside additional UI components (```Metadata``` UI component)
  9. In the ```Visualization Parameters``` UI component, choose the transfer function (currently only 1D is supported)
   and adjust runtime visualization parameters (e.g., you can change the interpolation method - choose ```trillinear```
   for best quality).
  10. The default TF is a 1D transfer function. A ````1D Transfer Function``` UI component should be visible in the
   bottom of the screen: 

      - Green line is for opacities (i.e., alpha) classification
      - Bottom gradient color band/texture is for colors (no alpha) classification
      - Changes are reflected realtime in the volumetric object visualization

## Project Structure

TODO

## Render Modes

CTVisualizer comes with a set of state-of-the-art rendering modes (i.e., different shaders) that might be suitable for
different input dataset characteristics (e.g., size, sparsity/homogeneity, anisotropy, etc.). Since the target dataset
size is in the range of hundreds of GBs, a lot has to be done in the Shaders and CPU-side code to efficiently handle
CPU-GPU communications. This has the unfortunate side effect of adding a lot of complexity.

### In Core (IC) Rendering Mode

Useful for datasets that fit within the available VRAM on the GPU. Employs no empty space skipping acceleration
structures. This is mainly used as a baseline to compare the performance of other rendering methods against.
Consequently, this is by far the simplest shader and sometimes, surprisingly, the fastest (especially for small
datasets).

### Out-of-Core (OOC) Virtual Memory (VM) Rendering Mode

Employs a software-implemented virtual memory scheme (analogous to that employed by operating systems) and a
multi-resolution, single-level (multi-level support is not yet implemented) page table hierarchy. Granularity of empty
space skipping and adaptive ray sampling is at the level of page table entries.

### Out-of-Core (OOC) Hybrid Rendering Mode

Employs a hybrid approach of a virtual memory scheme (same as in OOC VM rendering mode) and an octree-based subdivision
scheme for empty space skipping. Empty space skipping is achieved at the granularity of both: page table entries and
octree nodes. The octree is traversed on the GPU and subsequently adds additional overhead to the fragment shader
relative to that of OOC VM rendering mode.

## Known Issues

### Universal Rendering Pipeline Shader Issues

On URP, the out-of-core shaders do not work because of some unresolved UAV binding issue. Unity will spawn the
"Attempting to draw with missing UAV bindings" warning. It could be that additional steps have to be done when
writing custom shaders on URP. See this [repo][4] that details this bug.

This is a major setback especially for XR targets where foveated rendering is not supported on the build-in rendering
pipeline.

### Blank Screen When Cross-Compiling on Linux for the Magic Leap 2

When cross-compiling for the ML2 platform on Linux, upon starting the *ctvisualizer* app on an ML2 device, the
"Made in Unity" logo may not appear and instead a faint square is shown for a very short period of time after which
nothing is shown anymore (except the controller's ray). You might notice some OpenXR runtime error
(through adb logcat | grep "Unity") - this is probably caused by some weird bug in the Magic Leap 2 SDK plugin when it
is running on Linux. The bug may literally appear out of nowhere - you compile the app once and it runs fine, you change
*nothing* then compile again and it stops working.

To avoid this, **consider cross-compiling for the ML2 target on a Windows platform** (it is after all the most supported
platform by the ML2).

## License

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
See LICENSE.txt file for more info.

[1]: https://github.com/walcht/TextureSubPlugin
[2]: https://github.com/walcht/cvds
[3]: https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html
[4]: https://github.com/walcht/Unity-RWStructuredBuffer-Readback-Sample