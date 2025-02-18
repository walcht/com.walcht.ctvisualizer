# Unity CTVisualizer

<!--toc:start-->
- [Unity CTVisualizer](#unity-ctvisualizer)
  - [Installation](#installation)
  - [Usage](#usage)
  - [Project Structure](#project-structure)
  - [Optimization Techniques](#optimization-techniques)
    - [Empty space skipping](#empty-space-skipping)
    - [Early ray termination](#early-ray-termination)
  - [Performance Statistics](#performance-statistics)
  - [License](#license)
<!--toc:end-->

A Unity3D package for efficiently visualizing and manipulating large-scale
volumetric datasets.

## Installation

This project makes use of a [native C++ rendering plugin](https://github.com/walcht/TextureSubPlugin). To get started:

  1. follow the instuctions in [TextureSubPlugin](https://github.com/walcht/TextureSubPlugin) to compile the plugin for
     your target (Windows, Linux, or Android)
  2. install the CVDS Python converter from [here](https://github.com/walcht/cvds)

Tested on these Unity versions:

| Unity Version | OS             | Status             | Notes |
| ------------- | -------------- | ------------------ | ----- |
| 2022.3.17f1   | 22.04.1-Ubuntu | :white_check_mark: |       |
| 2022.3.17f1   | macOS 14.2.1   | :white_check_mark: |       |


## Usage 

UnityCT-Visualizer is a UI-centric application, i.e., all operations are mainly
done through the provided GUI. To visualize a CT/MRI dataset within the Unity Editor:

  1. convert your dataset into CVDS format using the [CVDS Python converter package](https://github.com/walcht/cvds)
  2. run the default provided Unity scene
  3. click on ```import``` to import the converted CVDS dataset 
  4. once the import is done, the volumetric object should appear along with additional UI components 
  5. in the ```Visualization Parameters``` UI component, choose the transfer function (currently only 1D is supported).
     you can also change the interpolation method (choose ```trillinear``` for best quality)
  6. the default TF is TF1D, its UI will be shown in the bottom:

      - green line is for opacities (i.e., alpha) classification
      - bottom gradient color band/texture is for colors (no alpha) classification
      - changes are real-time reflected in the volumetric object visualization

## Project Structure

TODO

## Optimization Techniques

### Empty space skipping
TODO

### Early ray termination

TODO

## Performance Statistics

TODO

## License

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
See LICENSE.txt file for more info.
