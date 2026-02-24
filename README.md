# RoomAcoustiC++: Real-time Acoustics Library - Unity Engine Interface

RoomAcoustiC++ (RAC) is a C++ library for real-time room acoustic modelling designed for psychoacoustic research and immersive audio applications.
RAC was developed as part of a PhD at the University of Surrey and is distributed open-source with the aim of encouraging and increasing accessibility to real-time acoustics across technical and non-technical research fields.
This repository provides modular C# scripts and resources for integrating RAC with the Unity projects.

The official documentation of RoomAcoustiC++, including the API used by Unity, is available at [https://roomacousticpp.readthedocs.io/en/latest/](https://roomacousticpp.readthedocs.io/en/latest/).

## How to use

Setting up a Unity project to use RAC involves four components: the manager, the acoustic mesh, the listener, and the sound sources.
There may be multiple sound sources, and they may be created or destroyed at runtime; the other three components are singletons, meaning there must be exactly one in the scene at all times.
These instructions give an overview of what they are, what they do, and how to set them up.
You can find further detail in each component's tooltips (hover your mouse over elements in the inspector).

### Fundamental components

#### The acoustics manager

Create an empty game object anywhere in your scene hierarchy, and assign a "RAC Audio Manager" component to it.
This component is defined by the `RACManager` class.
It acts as the "control panel" for the plugin: this is where you will specify all global settings for acoustic modeling and digital signal processing.
You will notice that some settings become "locked" during runtime, while others can be changed dynamically; function calls are also provided to change these settings through the code.

#### The environment mesh

Create another empty game object anywhere, and assign a "RAC Audio Mesh" component to it.
This component is defined by the `RACMeshLoader` class.
It loads and handles the 3D mesh which describes the acoustic environment &mdash; you may or may not render it visually as well.

This component assumes that you have pre-processed your environment using [the Python package for MoD-ART analysis](https://github.com/Matteo-Scerbo/MoD-ART).
The pre-processed mesh files must be placed in `PythonExports/YourEnvironmentName/` as shown in the following example.
Also, make sure the `RAVES-Unity` GitHub repository is cloned in the `Assets` folder as shown.
The first time your environment is loaded in the Unity editor, a file named `YourEnvironmentName.prefab` will be automatically generated in `ProcessedPrefabs`.

```
UnityProjectName/
├── Assets/
│   ├── RAVES-Unity/
│   │   └── [cloned GitHub repository]
│   ├── Resources/
│   │   ├── ProcessedPrefabs/
│   │   │   ├── YourEnvironmentName.prefab
│   │   │   ├── YourOtherEnvironmentName.prefab
│   │   │   └── [...]
│   │   ├── PythonExports/
│   │   │   ├── YourEnvironmentName/
│   │   │   │   ├── materials.csv
│   │   │   │   ├── mesh.mtl
│   │   │   │   ├── mesh.obj
│   │   │   │   ├── MoD-ART.csv
│   │   │   │   └── path_indexing.csv
│   │   │   ├── YourOtherEnvironmentName/
│   │   │   │   ├── materials.csv
│   │   │   │   ├── mesh.mtl
│   │   │   │   ├── mesh.obj
│   │   │   │   ├── MoD-ART.csv
│   │   │   │   └── path_indexing.csv
│   │   │   └── [...]
│   │   └── [...]
│   └── [...]
└── [...]
```

#### The listener

Choose the game object which will represent the listener's position and orientation (usually a child of the main camera), and assign a "RAC Audio Listener" component to it.
This component is defined by the `RACAudioListener` class.

Note that adding the "RAC Audio Listener" component will automatically add a regular "Audio Listener" component to the same object, as required.
There may only be one "Audio Listener" component in the scene, so make sure to remove or deactivate any other listeners from the scene.

#### The sound sources

Choose any number of game objects which will represent the sound sources' positions and orientations, and assign a "RAC Audio Source" component to each.
This component is defined by the `RACAudioSource` class.

Note that adding the "RAC Audio Source" component will automatically add a regular "Audio Source" component to the same object, as required.
Disregard the "Audio Source" component: all of its settings are overruled by the "RAC Audio Source" component, **including the audio clip**.
Select the desired audio clip and settings within the "RAC Audio Source" inspector instead.

### Additional components

There are other components which are not strictly necessary to operate RAC, but may be useful in advanced use cases.

#### Debugging

To access RAC's debugging features, you must add a "RAC Debug C++" component to a game object in the scene.
This component is defined by the `DebugCPP` class.
You will also need to select the "RAC_Debug" version of the plugin, using the drop-down selection at the top of the "RAC Audio Manager" inspector.
After applying the plugin selection, Unity will load the "RAC_Debug" DLL which includes debugging features.

#### Recording data

You can save impulse responses between specific positions by using the "IR Controller" component.
This component is defined by the `IRController` class.
In the "IR Controller" inspector, you can fill out lists of game objects representing different sources and listeners, as well as lists of configurations for early reverb, late reverb, and spatialization.
Then, during a running session, click "Run Impulse Responses": the script will produce impulse responses for all possible combinations of the listed settings, and save the audio files in a "persistent folder" (`Application.persistentDataPath`) [at the location specified in the Unity docs](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-persistentDataPath.html).
The script will also save MoD-ART residue values at the specified source and listener positions.

#### Other features

The repository includes a variety of additional features like real-time plotting of energy decay curves, control of sound sources' navigation through a minimap, UI bindings for runtime changes of acoustic settings, and more.
These advanced components are more involved, and best explained by example.
Contact us about some example Unity projects which make use of them.

## Citable references
First publication describing the architecture of RAC, its main features, and analysis of accuracy and real-time performance:
* Mannall J., Savioja L., Neidhardt A., Mason R. and De Sena E. "RoomAcoustiC++: An open-source room acoustic model for real-time audio simulations,” in Proc. AES Int. Conf. on Headphone Tech., Espoo, Finland, 2025

## Additional tools
In addition to this repository, there are other repositories with associated tools:
* RoomAcoustiCpp: C++ source code. Code available at: [https://github.com/jmannall/RoomAcoustiCpp](https://github.com/jmannall/RoomAcoustiCpp)
* MoD-ART: Python code for pre-processing environments. Code available at: [https://github.com/Matteo-Scerbo/MoD-ART](https://github.com/Matteo-Scerbo/MoD-ART)

## Credits
The software was developed by
* [Joshua Mannall](https://github.com/jmannall) ([Institute of Sound Recording, University of Surrey](https://iosr.surrey.ac.uk/)). Contact: j.mannall@surrey.ac.uk

## Aknowledgements
The project utilises the [3D-TuneIn Toolkit](https://github.com/3DTune-In/3dti_AudioToolkit) (3DTI) for binaural processing.
The forked 3dti_AudioToolkit repository (included as a submodule) includes all the required files for use with RAC.
Some small changes have been made for the purposes of compatibility between the source files.

The lock-free queue [concurrentqueue](https://github.com/cameron314/concurrentqueue) is used for multithreaded audio processing.

<!-- ## License
RoomAcoustiCpp is distributed under the GPL v3, a popular open-source license with strong copyleft conditions license.

If you license RoomAcoustiCpp under GPL v3, there is no license fee or signed license agreement: you just need to comply with the GPL v3 terms and conditions. See ROOMACOUSTICPP_LICENSE and LICENSE for further information. -->
