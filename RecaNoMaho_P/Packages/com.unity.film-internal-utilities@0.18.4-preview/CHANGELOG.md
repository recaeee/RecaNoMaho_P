# Changelog

## [0.18.4-preview] - 2023-03-27

### Fixed
* fix: disable Analytics on platforms that do not support Analytics

## [0.18.3-preview] - 2023-03-15

### Fixed
* internal-fix: turn ObjectUtility::Destroy to a generic function 

## [0.18.2-preview] - 2023-03-15

### Added
* internal: add EnumUtility.ToInspectorNameDictionary() 
* internal: add ObjectUtility.Destroy() with optional undo

### Fixed
* internal-fix: send actual package version for Analytics 

## [0.18.1-preview] - 2023-03-03

### Changed
* internal: open internals to ToonShader package 

## [0.18.0-preview] - 2023-03-02

### Added
* internal: add common classes for analytics
* internal: add ObjectUtility.DestroyImmediate() API
* internal: add SceneComponents.ForceUpdate() API
* internal: add UndoAndRefreshTimelineEditor() API for testing

### Changed
* opt: replace calls of FindObjectsOfType() with FindObjectsByType()

### Fixed
* fix: LayoutUtility errors on Unity 2023.x

### Removed
* remove: unused ObjectUtility code for Unity 2019 or earlier

## [0.17.0-preview] - 2022-12-21

### Added
* internal: add EnumerableExtensions
* internal: add TimelineUtility::DeleteInvalidMarkers() API
* internal: add TimelineClipExtensions::Contains() API
* internal: add EditorWindowExtensions.Resize() API

### Changed
* internal: change the access modifier of GetWindowSize() to internal

## [0.16.4-preview] - 2022-12-08

### Fixed
* fix: reset ClipData dictionary state when the playmode changed

## [0.16.3-preview] - 2022-12-08

### Added
* internal: add BaseTrackClipPopup class
* internal: add BitUtility.IsBitSet() function 
* internal: add TimelineUtility.TimeToFrame() function
* internal: add EditorWindowExtensions

### Fixed
* fix: hide dummy track for testing in the Timeline menu 
* fix: serialize ClipData based on order and use Dictionary for operational uses. 


## [0.16.2-preview] - 2022-12-02

### Fixed
* fix: store the hash code of the PlayableAsset as the key for the ClipData

## [0.16.1-preview] - 2022-11-23

### Added
* internal-feat: SceneComponents for caching

### Fixed
* fix: ensure that ClipData corresponds to the correct TimelineClip during serialization/deserialization

## [0.16.0-preview] - 2022-09-27

### Added
* internal: add ExposedReferenceEditorUtility class

### Changed
* package: upgrade minimum required Unity version to 2020.3 
* internal: make AssetEditorUtility.GetApplicationRootPath() as internal 

### Fixed
* internal: make sure the directory exists when calling AssetEditorUtility.CreateSceneAsset() 

## [0.15.2-preview] - 2022-07-13

### Fixed
* fix: MonoBehaviourSingleton errors when Configurable Enter Play Mode is turned on

## [0.15.1-preview] - 2022-05-17

### Fixed
* internal-fix: mark changed object as dirty in DrawUndoableGUI() and DrawScrollableTextAreaGUI() 

## [0.15.0-preview] - 2022-04-26

### Added
* internal: open internal FilmInternalUtilities API to Storyboard package

### Changed
* change: make JsonAttribute to internal 
* opt: optimize FindSceneComponents() for Unity 2020.3 and up

### Fixed
* fix: build error on Unity 2021.3.x due to RuntimeInitializeOnLoad being inapplicable to generic classes 

### Removed
* internal-remove: remove obsolete code in AssetUtility 
* internal-remove: remove BaseJsonSettings


## [0.14.2-preview] - 2022-04-14

### Changed
* internal: use Undo.DestroyObjectImmediate() if applicable inside ObjectUtility.Destroy() 

### Fixed
* fix: null check of PlayableAsset when initializing clip data

## [0.14.1-preview] - 2022-04-04

### Added
* internal: add YieldUtility and YieldEditorUtility

### Fixed
* fix: init clips earlier for tracks derived from BaseExtendedClipTrack
* internal: make functions in GUILayoutUtility to internal 

## [0.14.0-preview] - 2022-03-31

### Added
* internal: add a function to get the active TimelineClip given a set of clips and time
* internal: add GUILayoutUtility
* internal: add a utility function to do operations on ExposedReference 
* internal: add a function to get ClipData from TimelineClip
* internal: add a function to find all descendants of a GameObjec

### Changed
* deps: update dependency to com.unity.timeline@1.2.18

## [0.13.0-preview] - 2022-02-07

### Added
* internal: BaseJsonSingleton class 
* internal: add API to convert paths relative to "Assets" and "Resources"

### Changed
* refactor: make the constructor of PackageVersion() to private 

### Fixed
* fix: only try to create directory if applicable when serializing to json

## [0.12.5-preview] - 2022-01-28

### Added
* internal: add an internal API to create a scene asset

### Changed
* internal: open FilmInternalUtilities to GoQL 

### Fixed
* fix: check if a callback has been set in fields added by UIElementsEditorUtility.AddField() before invoking

## [0.12.4-preview] - 2022-01-07

### Changed
* internal: add Move() extension to List 

## [0.12.3-preview] - 2022-01-06

### Changed
* internal: add a new API to add UIElements field with a className argument
* internal: open the editor assembly of FilmInternalUtilities to SelectionGroups.Editor 

### Fixed
* fix: check null or empty input string in IsPathNormalized() 


## [0.12.2-preview] - 2021-11-05

### Added
* internal: add a utility function to load asset by GUID
* internal: add a utility function to find asset paths

### Changed
* considerpaths  under "Library" to be normalized 
* internal: move EditorUtility.WaitForFrames() to the Editor assembly
* internal: make GetDirectoryName() return a string using '/' as the directory separator

### Fixed
* fix: remove ITimelineClipAsset requirement from CreateTrackAndClip() 

## [0.12.1-preview] - 2021-10-28

### Changed
* considerpaths  under "Library" to be normalized 

## [0.12.0-preview] - 2021-10-28

### Added
* internal: add GameObjectUtility to find/create GameObjects by path
* internal: add EnumUtility function to convert enum to a list of inspector names
* internal: add EnumUtility function to convert enum values to a list
* internal: add TransformExtensions and find/create child and set the parent of a Transform
* internal: add functions to add fields in UIElementsEditorUtility 
* internal: add EditorTestUtility.WaitForFrames() 
* internal: add BaseJsonSettings
* internal: add AnimationCurveExtension
* internal: add a TimelineEditor utility function to show TimelineClip in the inspector
* internal: add a TimelineEditorReflection function to create a TimelineClip on Track
* internal: add TimelineEditor utility functions to show/refresh TimelineWindow
* internal: add SerializedDictionary class
* internal: add MonoBehaviourSingleton class

### Changed
* consider files under "Packages" to be normalized as well 
* make path functions in AssetUtility to be obsolete, and create their replacements in AssetEditorUtility
* let PackageVersion handle "x" token 
* add default parameters to OneTimeLogger::Update() 
* call CreateClipOnTrack() reflection code in TimelineEditorUtility.CreateTrackAndClip(), which will trigger ClipEditor.OnCreate()
* set IAnimationCurveOwner to internal 
* open FilmInternalUtilities.Editor assembly to AnimeToolbox runtime code
* make CreateGameObjectWithComponent() obsolete

## [0.11.1-preview] - 2021-10-18

### Fixed
* fix: GetOrAddComponent() was not working properly


## [0.11.0-preview] - 2021-09-02

### Added
* feat: add utilities to create/delete Timeline assets
* feat: add a RenderTexture extension to write to a file

### Changed
* make EditorGUIDrawerUtility::DrawUndoableGUI return success or not (bool)
* deps: make FilmInternalUtilities directly depend on Timeline package

## [0.10.2-preview] - 2021-08-17

### Changed
* test against 2021.2 too

### Fixed
* ensure that FilmInternalUtilities works on all platforms

## [0.10.1-preview] - 2021-07-01

### Changed
* make TimelineClipExtensions to internal

### Fixed
* fix warnings when using Timeline 1.6.x

## [0.10.0-preview] - 2021-07-01

### Added
* internal: add ListExtensions class with RemoveNullMembers() function 
* internal: add AssetUtility.IsAssetPath() 
* internal: add TimelineUtility class
* internal: add forceImmediate parameter to ObjectUtility::Destroy()

### Changed
* internal: open internals to com.unity.selection-groups
* refactor: simplify DrawFolderSelectorGUI() and DrawFileSelectorGUI() 

### Fixed
* fix:  NormalizeAssetPath() to normalize paths under the project path

## [0.9.0-preview] - 2021-04-15

### Added
* internal: EditorGUIDrawerUtility::DrawScrollableTextAreaGUI()
* internal: OneTimeLogger class to do logging once

### Changed
* internal: Simplify EditorGUIDrawerUtility::DrawUndoableGUI()

## [0.8.4-preview] - 2021-03-22

### Changed
* internal: refactor virtual methods in timeline-related classes

## [0.8.3-preview] - 2021-03-22

### Added
* internal: add ObjectUtility utility script and its FindSceneComponents method 

### Changed
* internal: change the functions names for serialization in BaseClipData 

## [0.8.2-preview] - 2021-03-03

### Changed
* internal: open internals of FilmInternalUtilities to MaterialSwitch

## [0.8.1-preview] - 2021-03-01

### Added
* internal: add TimelineClipExtensions 

## [0.8.0-preview] - 2021-02-24

### Added
* add ExtendedClipEditorUtility, containing utility functions to modify curves on ClipData or TimelineClip

### Changed
* simplify BaseExtendedClipTrack

## [0.7.1-preview] - 2021-02-18

### Changed
* change some functions in BaseClipData into abstract functions explicitly

## [0.7.0-preview] - 2021-02-10

### Added
* add DrawUndoableGUI() function to draw GUI which can be undoable

## [0.6.0-preview] - 2021-01-29

### Added
* add scripts for adding data to TimelineClip (only loaded when a project uses Timeline)

## [0.5.1-preview] - 2021-01-26

### Fixed
* fix license
* fix warning in changelogs

## [0.5.0-preview] - 2021-01-18

### Changed
* rename package name to FilmInternalUtilities
* change all public APIs to internal, and open them only to known film assemblies

## [0.4.0-preview] - 2021-01-08

### Added
* add a PackageVersion class to parse package version (semver) 

### Changed
* change the class names of PackageRequest related classes

## [0.3.1-preview] - 2020-12-14

### Changed
* include UIElements as a dependency of AnimeToolbox
* cleanup internal functions 

## [0.3.0-preview] - 2020-10-29

### Added
* add ObjectExtensions, RenderTextureExtensions, Texture2DExtensions classes 
* add PathUtility::GenerateUniqueFolder() utility function
* add a notifier to notify users to restart Unity after script compilation


## [0.2.1-preview] - 2020-10-13

### Removed
* remove unsupported/unused window

## [0.2.0-preview] - 2020-10-01

### Added
* add utility functions from StreamingImageSequence
* add utility functions from MeshSync (AssetUtility, AssetEditorUtility, EditorGUIDrawerUtility) 
* doc: add package badge in the top readme

### Changed
* test com.unity.anime-toolbox against Unity 2020 and 2021
* chore: use new Yamato conf template and reapply the existing settings

### Fixed
* fix package warnings

### Removed
* delete unused legacy functions
* remove dependency to recorder. No longer required.

## [0.1.6-preview] - 2020-08-26

### Changed
* update package info 

### Fixed
* fix test code on Linux
* fix doc warnings


## [0.1.5-preview] - 2020-08-14

### Removed
* remove obsolete/unsupported tracks from the menu

## [0.1.4-preview] - 2020-07-27

### Changed
* make UIElementsUtility into a public class 

## [0.1.3-preview] - 2020-07-27

### Added
* add UIElementsUtility which provides several utility UIElements-related utility functions
* add more error handling in FileUtility 

### Fixed
* fix build error when building applications


## [0.1.2-preview] - 2020-05-20

### Added
* test: add PathUtilityTest for testing PathUtility

### Changed
* open UIElementsEditorUtility to public	
* open PathUtility functions to public
* rename runtime assembly to Unity.AnimeToolbox without Runtime

## [0.1.1-preview] - 2020-05-20

### Changed
* change dependency of com.unity.recorder to version 2.1.0-preview.1


## [0.1.0-preview] - 2020-05-19

### Added
* add new utility scripts (FileUtility, PathUtility, UIElementsEditorUtility)
* add PackageRequest classes 

### Changed
* rename editor namespace to Unity.AnimeToolbox.Editor

## [0.0.2-preview] - 2020-04-08

### Added
* The first release of *Anime Toolbox \<com.unity.anime-toolbox\>*.

