# Unity CTVisualizer

<!--toc:start-->
- [Unity CTVisualizer](#unity-ctvisualizer)
  - [Installation \& Build Instructions](#installation--build-instructions)
    - [Build Instructions for the Magic Leap 2 Platform](#build-instructions-for-the-magic-leap-2-platform)
      - [Potential Issues on the Magic Leap 2 Device](#potential-issues-on-the-magic-leap-2-device)
  - [Usage](#usage)
  - [Project Structure](#project-structure)
  - [Render Modes](#render-modes)
    - [In Core (IC) Rendering Mode](#in-core-ic-rendering-mode)
    - [Out-of-Core (OOC) Virtual Memory Rendering Mode](#out-of-core-ooc-virtual-memory-rendering-mode)
    - [Out-of-Core (OOC) Hybrid Rendering Mode](#out-of-core-ooc-hybrid-rendering-mode)
  - [License](#license)
<!--toc:end-->

A Unity3D package for efficiently visualizing and manipulating very large (in
the range of 100GBs) CT/MRI volumetric datasets.

## Installation & Build Instructions

The project is provided as a separate Unity package that can be easily added to your project by:

  1. Window -> Package Manager -> (Top left + icon) -> Install package from Git URL -> Add this repo's link:

      ```
      https://github.com/walcht/com.walcht.ctvisualizer.git
      ```
    
      After having imported CTVisualizer, you may encounter some missing dependency(ies) issues. Make sure to close and
      open the Unity editor to trigger a custom resolver for the Git package dependencies (Unity's default package
      manager does not support Git packages. Yeah, you read that right.). In case the missing dependency packages are
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
| 6000.0.40f1   | Ubuntu 22.04.5 | Magic Leap 2    | :white_check_mark: |       |


### Build Instructions for the Magic Leap 2 Platform

To build for the Magic Leap 2 AR device:

  1. Connect the device to a Linux-based machine<sup>1</sup>
  2. Follow the instructions on this [repository][1] to build the native plugin for the ML2 device
  3. Import the *magicleap2* sample scene and build it
  4. After having finished the building process, navigate to the build directory and run:
   
      ```shell
      adb install ctvisualizer.x86_64.apk
      ```

  5. Copy your converted [CVDS]() dataset(s) into the [Application.persistentDataPath][3] on the attached ML2 device using:
  
      ```shell
      adb push <path-to-cvds-dataset-folder> /storage/emulated/0/Android/data/com.walcht.ctvisualizer/files/
      ```

  6. You can also optionally copy other resources to the same directory above such as: serialized transfer functions,
   serialized visualization parameters, etc.

  7. Run the just-installed *ctvisualizer* app on the ML2<sup>2</sup>


 <sup>1</sup>: I couldn't get cross-compiling to work on a Windows machine - the problem is mainly related to compiling the
 native plugin for a linux target on a Windows host machine.

 <sup>2</sup>: For debugging potential issues on the ML2, before starting the *ctvisualizer* app, run:

    ```shell
    adb shell logcat | grep "Unity"
    ```

    Make sure to keep an eye for errors and exceptions (especially OpenXR-related thrown exceptions)

#### Potential Issues on the Magic Leap 2 Device

Upon starting the *ctvisualizer* app on an ML2 device, the "Made in Unity" logo may not appear and instead a faint
square is shown for a very short period of time after which nothing is shown anymore (except the controller's ray).

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

### Out-of-Core (OOC) Virtual Memory Rendering Mode

Employs a software virtual memory scheme (analogous to that employed my operating systems) and a multi-resolution,
single-level (multi-level support is currently in progress) page table hierarchy. Granularity of empty space skipping
and adaptive ray sampling is at the level of page table entries.

### Out-of-Core (OOC) Hybrid Rendering Mode

Employs a hybrid approach of a virtual memory scheme (same as in OOC VT rendering mode) and an octree-based subdivision
scheme for empty space skipping. Empty space skipping is achieved at the granularity of both: page table entries and
octree nodes. The octree is traversed on the GPU and subsequently adds additional overhead to the fragment shader
relative to that of OOC VT rendering mode.

## License

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
See LICENSE.txt file for more info.

[1]: https://github.com/walcht/TextureSubPlugin
[2]: https://github.com/walcht/cvds
[3]: https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html