# Unity CTVisualizer

A Unity3D package/plugin for efficiently visualizing and manipulating very large
(in the range of 100GBs) CT/MRI volumetric datasets. The package comes with a set
of samples for different target platforms (e.g., desktop, Magic Leap 2).

![Unity CT Visualizer Snake Dataset Showcase][ctvisualizer-snake-showcase]

This project started as an implementation of the latest state-of-the-art direct
volume rendering techniques adjusted for immersive environments for my M.Sc. of
Computer Science thesis.

## Showcase

<details>

<summary>Show videos (slows down rendering and scrolling)</summary>

Brick granularity loading to the GPU (using TextureSubPlugin):

https://github.com/user-attachments/assets/5cee257e-f52a-488a-be92-07cfdbdcb2d6

Camera going through the bbox is also supported:

https://github.com/user-attachments/assets/17d0eaa1-86be-46ac-8c04-e64828dd9006

1D transfer function UI with serialization/deserialization support:

https://github.com/user-attachments/assets/4e02b802-0175-4c3d-af5e-e04a65e49034

LoD (level of details) optimization technique of CTVisualizer:

https://github.com/user-attachments/assets/4d10bece-6b18-41a2-afb3-690c7837de49

</details>

Out-of-core rendering of the Enigma dataset (about 8.00GBs) using around 600MBs of VRAM:

![Unity CT Visualizer Enigma Dataset Showcase][ctvisualizer-enigma-showcase]

Out-of-core rendering of the turtle dataset (about 1.40GBs) using around 440MBs of VRAM:

![Unity CT Visualizer Turtle Dataset Showcase][ctvisualizer-turtle-showcase]

In-core rendering of the Fish dataset (about 300MBs):

![Unity CT Visualizer Fish Dataset Showcase][ctvisualizer-fish-showcase]

## Installation & Build Instructions

The project is provided as a separate Unity package that can be easily added to your project by:

  1. Window -> Package Manager -> (Top left + icon) -> Install package from Git URL -> Add this repo's link:

      ```
      https://github.com/walcht/com.walcht.ctvisualizer.git
      ```
    
      After having imported CTVisualizer, you may encounter some missing dependency(ies) issues. Make sure to close and
      reopen the Unity editor to trigger a custom resolver for the Git package dependencies (Unity's default package
      manager does not support Git packages. Yeah, you read that right...). In case the missing dependency packages are
      not resolved, navigate to ```package.json -> git-dependencies``` and install them manually.

  1. This project makes use of a [native C++ rendering plugin][1] to augment Unity's limited graphics API to be able to
   create larger-than-2GBs textures and upload chunks to them. Follow the instructions in [TextureSubPlugin][1] to
   compile the plugin for your target platform (Windows, Linux, MagicLeap2, or Android).

  2. CTVisualizer expects input datasets in the form of [Chunked Volumetric DataSet (CVDS)][2]. A separate, offline,
   Python CVDS converter is needed and can be installed from [here][2].

Tested on these Unity versions for these target platforms:

| Unity Version | Host Platform  | Target Platform | Status             | Notes                                             |
| ------------- | -------------- | --------------- | ------------------ | ------------------------------------------------- |
| 6000.0.40f1   | Windows 10     | Windows 10      | :white_check_mark: |                                                   |
| 6000.0.40f1   | Ubuntu 22.04.5 | Ubuntu 22.04.5  | TODO               |                                                   |
| 6000.0.40f1   | Ubuntu 22.04.5 | Magic Leap 2    | :white_check_mark: | might get a black screen - see Known Issues below |
| 6000.0.40f1   | Windows 10     | Magic Leap 2    | :white_check_mark: |                                                   |


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
  10. The default TF is a 1D transfer function. A ```1D Transfer Function``` UI component should be visible in the
   bottom of the screen: 

      - Green line is for opacities (i.e., alpha) classification
      - Bottom gradient color band/texture is for colors (no alpha) classification
      - Changes are reflected realtime in the volumetric object visualization


## Render Modes

CTVisualizer comes with a set of state-of-the-art rendering modes (i.e., different shaders) that might be suitable for
different input dataset characteristics (e.g., size, sparsity/homogeneity, anisotropy, etc.). Since the target dataset
size is in the range of hundreds of GBs, a lot has to be done in the Shaders and CPU-side code to efficiently handle
CPU-GPU communications. This has the unfortunate side effect of adding a lot of complexity.

The rendering modes are:
- [DVR IC: DVR In Core](#dvr-in-core-ic-rendering-mode)
- [DVR OOC\_VM: DVR Out-of-Core Virtual Memory](#dvr-out-of-core-ooc-virtual-memory-vm-rendering-mode)
- [DVR OOC\_HYBRID: DVR Out-of-Core Hybrid (VM + Octree Acceleration Structure)](#dvr-out-of-core-ooc-hybrid-rendering-mode)

### DVR In Core (IC) Rendering Mode

Useful for datasets that fit within the available VRAM on the GPU. Employs no empty space skipping acceleration
structures. This is mainly used as a baseline to compare the performance of other rendering methods against.
Consequently, this is by far the simplest shader and sometimes the fastest (especially for small datasets).

---

All DVR rendering modes implement this basic raymarching algorithm (OOC rendering modes adjust it so
that LoDs can be used):

![basic raymarching technique](https://github.com/user-attachments/assets/b3b7f6d9-db24-4db6-a268-2066563190c6)

Assuming a perspective camera, blue points on the near clipping
plane are fragment centers. A view ray is cast through each of these points.
Blue line is an example of a cast view ray that computes the color of its
fragment *f*. Blue points on the blue line are volume-ray intersection sample
points. Green points refer to sample points that should, ideally, contribute
to the final color of fragment *f*. Red points are sample points that should
not contribute to the final color of fragment *f*.

---

The implementation of this basic IC rendering mode can be found in this shader: [ic\_dvr\_shader](Runtime/Shaders/ic_dvr_shader.shader)

### DVR Out-of-Core (OOC) Virtual Memory (VM) Rendering Mode

Employs a software-implemented virtual memory scheme (analogous to that employed by operating systems) and a
multi-resolution, single-level (multi-level support is not yet implemented) page table hierarchy. Granularity of empty
space skipping and adaptive ray sampling is at the level of page table entries.

---

OOC rendering modes implement this LoD-based raymarching technique (ideally with trilinear interpolation):

![LoD-based raymarching technique employed by OOC rendering modes](https://github.com/user-attachments/assets/86f8c3a3-ce3f-4f39-90d7-942ba03567b7)

The blue line is a cast view ray that computes the color of its fragment *f*. Blue points on that line
are volume-ray intersection sample points. Light grey dotted lines denote a
brick’s spatial extent with a constant size of 2<sup>2</sup>. Larger points denote lower
LoD. The green points are samples that should, ideally, contribute to *f*.
Conversely, red points are samples that should, ideally, not contribute to *f*.
Purple points are the actual sampled points. The sampling rate is halved
once the ray enters a lower LOD brick region.

---

The scheme for the multi-resolution page table hierarchy with no intermediary virtualized page tables is as follows:

![multi-resolution page table directory](https://github.com/user-attachments/assets/f30c5499-d6a6-445c-8242-b3c156e19167)

The blue arrow denotes a cast view ray along which two sample points $s_{0} = (p_{0}, l_{0})$ and $s_{1} = (p_{1}, l_{1})$ have
to be sampled and require bricks in LOD 0 and 1, respectively. To sample $s_{0}$, the page directory for $\text{LOD} = 0$ is sampled,
using nearest neighbor interpolation, at the virtual coordinates $p_{0}$. The fetched page directory entry provides $(x, y)$ offset
into the brick cache to where the mapped brick is (or page, assuming it is at all mapped to begin with). Using the $x_{0}$ and
$y_{0}$ offsets within the brick, the brick cache is then sampled at location $(x + x_{0}, y + y_{0})$ yielding the final sampled
value. Similar process occurs for sample point $s_{1}$, the only difference is that the requested brick is of $\text{LOD} = 1$ and,
therefore, the associated page directory is used to fetch the brick location in cache not at $p_{1}$ but rather at an adjusted
$p_{1, \text{LOD} = 1}$ to accommodate for the added padding by the bricks in the corresponding resolution level.

---

The page directory (i.e., top-level page table) is implemented as follows:

![page directory implementation details](https://github.com/user-attachments/assets/59bf243c-3dc1-48d4-8c97-258ff86fa8fb)

The page directories for different resolution levels are all implemented within a single 3D texture. For this reason,
base offset for these subpage directories have to be provided for the shader code.
*_PageDirBase\[LOD\]* refer to said offsets. In this example, the page directory directly manages the brick cache which is more
efficient for sufficiently large volumes than a multi-level hierarchy that adds potential additional indirection costs. Dashed
area denotes wasted texture memory.

It is up to the user to define what homogenous means. A ```homogeneity tolerance``` parameter can be adjusted which defines
which regions are homogeneous based on whether the difference between their largest and smallest values is less than or equal
to this tolerance parameter.

---

The implementation of this VM-based OOC rendering mode can be found in this shader: [ooc\_vm\_shader](Runtime/Shaders/ooc_dvr_pt_shader.shader)

### DVR Out-of-Core (OOC) Hybrid Rendering Mode

Employs a hybrid approach of a virtual memory scheme (same as in OOC VM rendering mode) and an octree-based subdivision
scheme for empty space skipping. Empty space skipping is achieved at the granularity of both: page table entries and
octree nodes.

The idea here (at least theoretically), is that using an octree acceleration structure allows us to potentially skip
larger regions all together (i.e., in a single step contrary to skipping at a PT entry granularity for VM-only rendering
mode). The octree is traversed on the GPU and subsequently adds additional overhead to the fragment shader
relative to that of OOC VM rendering mode (i.e., you essentially end up with one more nested for loop which worsens the
performance).

---

An overview of the implemented Residency Octree is as follows:

![Residency Octree overview](https://github.com/user-attachments/assets/0de664e3-4931-4b54-891e-497f432fcde4)

In the middle is the 2D equivalent of the octree. On the right is the spatial extent of the first
node of each level of the octree. On the left is the bricks residency for different resolution levels.
Octree depth and resolution hierarchy are independent.

---

The viewing ray $\vec{r}$ is described by the parametric equation:

$$\vec{r} = \vec{o} + t\vec{d}$$

where $\vec{o}$ is the origin of the ray in object space and $\vec{d}$ is its normalized direction, and $t \in \mathbb{R}$.

In a simplified high level view, given a cast view ray, for each sample along its volume intersection, the residency octree
works as follows:

1. **LOD selection** - the LOD is chosen according to view parameters.

1. **Sampling rate adjustment** - the sampling rate is adjusted according to the selected LOD.

1. **Maximal octree traversal depth selection** - the maximal traversal depth is chosen based
   on the previously adjusted sampling rate. Ideally, the smallest skippable node should be
   significantly larger than the current step size.

1. **Octree traversal** - the octree is traversed up to either:

  1. **Reaching a homogeneous node** - at which point the color contribution is retrieve from
     the node (either minimum or maximum), the node is skipped, and the algorithm moves back
     to *step 1)*.
     
  1. **Reaching a completely unmapped node** - at which point the brick associated with the
     current sample point is requested, the node is skipped, and the algorithm moves back to *step 1)*.
     
  1. **Reaching previously set maximal traversal depth** - at which point a try at fetching the
     associated brick with the current sample point is performed. Upon success, if the brick is homogeneous then
     the brick is sampled once and skipped, otherwise it is sampled up to its boundary. In both cases the algorithm
     moves back to *step 1)*. Upon failure, an alternative brick is optionally requested. In case no alternative
     brick is available, the brick is skipped and the algorithm moves back to *step 1)*. If an alternative brick is
     available, it is sampled up to the boundary of the original brick and the algorithm moves back to *step 1)*.

---

Each residency octree node is implemented as the following struct:

![Residency Octree node struct](https://github.com/user-attachments/assets/9bbba9ad-2acb-4ca4-80bf-4f6cfffb9ba6)

The side length is stored as *side\_halved* to avoid additional runtime subdivisions and computations. The homogeneity
of a node is determined by comparing the min and max of the *data* field.

---

The implementation of this hybrid OOC rendering mode can be found in this shader: [ooc\_hybrid\_shader](Runtime/Shaders/ooc_dvr_hybrid_shader.shader)

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

Or just avoid the ML2 device altogether. It is a dead platform after all.

## Dataset Sources

Large (> 30 GBs) CT/MRI datasets are extremely hard to find on the internet. For small/medium sized datasets,
consider the following sources:

- **MorphoSource**: https://www.morphosource.org/

## License

MIT License. See LICENSE.txt file for more information.

[1]: https://github.com/walcht/TextureSubPlugin
[2]: https://github.com/walcht/cvds
[3]: https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html
[4]: https://github.com/walcht/Unity-RWStructuredBuffer-Readback-Sample
[ctvisualizer-enigma-showcase]: https://raw.githubusercontent.com/walcht/walcht/refs/heads/master/assets/images/ctvisualizer-enigma-showcase.jpg
[ctvisualizer-snake-showcase]: https://raw.githubusercontent.com/walcht/walcht/refs/heads/master/assets/images/ctvisualizer-snake-showcase.jpg
[ctvisualizer-turtle-showcase]: https://raw.githubusercontent.com/walcht/walcht/refs/heads/master/assets/images/ctvisualizer-turtle-showcase.jpg
[ctvisualizer-fish-showcase]: https://raw.githubusercontent.com/walcht/walcht/refs/heads/master/assets/images/ctvisualizer-fish-showcase.jpg
